#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenRA.Graphics;

namespace OpenRA.Platforms.Default
{
	class Shader : ThreadAffine, IShader
	{
		const string IncludeStart = "#Include:";
		const string IncludeEnd = "#End Include";

		readonly Dictionary<string, int> samplers = new Dictionary<string, int>();
		readonly Dictionary<int, int> legacySizeUniforms = new Dictionary<int, int>();
		readonly Dictionary<int, ITexture> textures = new Dictionary<int, ITexture>();
		readonly Queue<int> unbindTextures = new Queue<int>();
		readonly uint program;
		readonly IShaderBindings bindings;
		protected uint CompileShaderObject(int type, string name)
		{
			var ext = type == OpenGL.GL_VERTEX_SHADER ? "vert" : (type == OpenGL.GL_FRAGMENT_SHADER ? "frag" : "geom");
			var filename = name + "." + ext;
			string code;

			if (Game.ModData != null && Game.ModData.DefaultFileSystem.TryOpen(filename, out var stream))
			{
				code = stream.ReadAllText();
				stream.Dispose();
			}
			else
				code = File.ReadAllText(Path.Combine(Platform.EngineDir, "glsl", filename));

			// var version = OpenGL.Profile == GLProfile.Embedded ? "300 es" :
			// 	OpenGL.Profile == GLProfile.Legacy ? "120" : "140";
			var version = "300 es";

			code = code.Replace("{VERSION}", version);

			// find shader include
			if (code.Contains(IncludeStart))
			{
				Dictionary<string, string> includeContents = new Dictionary<string, string>();
				int idx = code.IndexOf(IncludeStart);
				code = code.Remove(idx, IncludeStart.Length);
				int last = code.IndexOf(IncludeEnd);
				code = code.Remove(last, IncludeEnd.Length);

				string[] includes = code.Substring(idx, last - idx).Split('\n');
				code = code.Remove(idx, last - idx);
				for (int i = includes.Length - 1; i > -1; i--)
				{
					var fname = includes[i].Trim();
					if (string.IsNullOrEmpty(fname))
						continue;

					string insert;

					if (Game.ModData != null && Game.ModData.DefaultFileSystem.TryOpen(fname, out var s))
					{
						insert = s.ReadAllText();
						s.Dispose();
					}
					else
						insert = File.ReadAllText(Path.Combine(Platform.EngineDir, "glsl", fname));

					// code = code.Insert(idx, insert + "\n");
					includeContents.Add(fname, insert);
				}

				foreach (var (ins, content) in includeContents)
				{
					code = code.Replace("{" + ins + "}", content + "\n");
				}
			}

			var shader = OpenGL.glCreateShader(type);
			OpenGL.CheckGLError();
			unsafe
			{
				var length = code.Length;
				OpenGL.glShaderSource(shader, 1, new string[] { code }, new IntPtr(&length));
			}

			OpenGL.CheckGLError();
			OpenGL.glCompileShader(shader);
			OpenGL.CheckGLError();
			OpenGL.glGetShaderiv(shader, OpenGL.GL_COMPILE_STATUS, out var success);
			OpenGL.CheckGLError();
			if (success == OpenGL.GL_FALSE)
			{
				OpenGL.glGetShaderiv(shader, OpenGL.GL_INFO_LOG_LENGTH, out var len);
				var log = new StringBuilder(len);
				OpenGL.glGetShaderInfoLog(shader, len, out _, log);

				Log.Write("graphics", "GL Info Log:\n{0}", log.ToString());
				throw new InvalidProgramException($"Compile error in shader object '{filename}'");
			}

			return shader;
		}

		public Shader(IShaderBindings bindings)
		{
			// var vertexShader = CompileShaderObject(OpenGL.GL_VERTEX_SHADER, name);
			// var fragmentShader = CompileShaderObject(OpenGL.GL_FRAGMENT_SHADER, name);
			this.bindings = bindings;

			var vertexShader = CompileShaderObject(OpenGL.GL_VERTEX_SHADER, bindings.VertexShaderName);
			var fragmentShader = CompileShaderObject(OpenGL.GL_FRAGMENT_SHADER, bindings.FragmentShaderName);
			uint geometryShader = 0;
			bool hasGeometryShader = false;
			if (bindings.GeometryShaderName != null)
			{
				geometryShader = CompileShaderObject(OpenGL.GL_GEOMETRY_SHADER, bindings.GeometryShaderName);
				hasGeometryShader = true;
			}

			// Assemble program
			program = OpenGL.glCreateProgram();
			OpenGL.CheckGLError();

			foreach (var attribute in bindings.Attributes)
			{
				OpenGL.glBindAttribLocation(program, attribute.Index, attribute.Name);
				OpenGL.CheckGLError();
			}

			if (OpenGL.Profile == GLProfile.Modern)
			{
				OpenGL.glBindFragDataLocation(program, 0, "fragColor");
				OpenGL.CheckGLError();
			}

			OpenGL.glAttachShader(program, vertexShader);
			OpenGL.CheckGLError();
			OpenGL.glAttachShader(program, fragmentShader);
			OpenGL.CheckGLError();
			if (hasGeometryShader)
			{
				OpenGL.glAttachShader(program, geometryShader);
				OpenGL.CheckGLError();
			}

			OpenGL.glLinkProgram(program);
			OpenGL.CheckGLError();
			OpenGL.glGetProgramiv(program, OpenGL.GL_LINK_STATUS, out var success);
			OpenGL.CheckGLError();
			if (success == OpenGL.GL_FALSE)
			{
				OpenGL.glGetProgramiv(program, OpenGL.GL_INFO_LOG_LENGTH, out var len);

				var log = new StringBuilder(len);
				OpenGL.glGetProgramInfoLog(program, len, out _, log);
				Log.Write("graphics", "GL Info Log:\n{0}", log.ToString());
				throw new InvalidProgramException($"Link error in shader program '{bindings.VertexShaderName}' and '{bindings.FragmentShaderName}'");
			}

			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();

			OpenGL.glGetProgramiv(program, OpenGL.GL_ACTIVE_UNIFORMS, out var numUniforms);

			OpenGL.CheckGLError();

			var nextTexUnit = 0;
			for (var i = 0; i < numUniforms; i++)
			{
				var sb = new StringBuilder(128);
				OpenGL.glGetActiveUniform(program, i, 128, out _, out _, out var type, sb);
				var sampler = sb.ToString();
				OpenGL.CheckGLError();

				if (type == OpenGL.GL_SAMPLER_2D || type == OpenGL.GL_SAMPLER_2D_ARRAY)
				{
					samplers.Add(sampler, nextTexUnit);

					var loc = OpenGL.glGetUniformLocation(program, sampler);
					OpenGL.CheckGLError();
					OpenGL.glUniform1i(loc, nextTexUnit);
					OpenGL.CheckGLError();

					if (OpenGL.Profile == GLProfile.Legacy)
					{
						var sizeLoc = OpenGL.glGetUniformLocation(program, sampler + "Size");
						if (sizeLoc >= 0)
							legacySizeUniforms.Add(nextTexUnit, sizeLoc);
					}

					nextTexUnit++;
				}
			}
		}

		public void PrepareRender()
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();

			for (int i = 0; i < 16; i++)
			{
				OpenGL.glVertexAttribDivisor(i, 0);
				OpenGL.CheckGLError();
				OpenGL.glDisableVertexAttribArray(i);
				OpenGL.CheckGLError();
			}

			// bind the textures
			foreach (var kv in textures)
			{
				var texture = (ITextureInternal)kv.Value;

				// Evict disposed textures from the cache
				if (OpenGL.glIsTexture(texture.ID))
				{
					OpenGL.glActiveTexture(OpenGL.GL_TEXTURE0 + kv.Key);
					if (texture is TextureArray)
						OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D_ARRAY, texture.ID);
					else
						OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, texture.ID);

					// Work around missing textureSize GLSL function by explicitly tracking sizes in a uniform
					if (OpenGL.Profile == GLProfile.Legacy && legacySizeUniforms.TryGetValue(kv.Key, out var param))
					{
						OpenGL.glUniform2f(param, texture.Size.Width, texture.Size.Height);
						OpenGL.CheckGLError();
					}
				}
				else
					unbindTextures.Enqueue(kv.Key);
			}

			while (unbindTextures.Count > 0)
				textures.Remove(unbindTextures.Dequeue());

			OpenGL.CheckGLError();
		}

		public void SetTexture(string name, ITexture t)
		{
			VerifyThreadAffinity();
			if (name == "boneAnimTexture")
			{
				if (t == null)
					throw new Exception("boneAnimTexture is null");

				if (samplers.TryGetValue(name, out var texUnit))
					textures[texUnit] = t;
				else
				{
					throw new Exception("boneAnimTexture not get");
				}
			}
			else
			{
				if (t == null)
				{
					throw new Exception("Texture: " + name + " is Null, Can't Set Texture");
				}

				if (samplers.TryGetValue(name, out var texUnit))
					textures[texUnit] = t;
			}
		}

		public void SetBool(string name, bool value)
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();
			OpenGL.glUniform1i(param, value ? 1 : 0);
			OpenGL.CheckGLError();
		}

		public void SetInt(string name, int value)
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();
			OpenGL.glUniform1i(param, value);
			OpenGL.CheckGLError();
		}

		public void SetFloat(string name, float value)
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();
			OpenGL.glUniform1f(param, value);
			OpenGL.CheckGLError();
		}

		public void SetVec(string name, float x)
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();
			OpenGL.glUniform1f(param, x);
			OpenGL.CheckGLError();
		}

		public void SetVec(string name, float x, float y)
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();
			OpenGL.glUniform2f(param, x, y);
			OpenGL.CheckGLError();
		}

		public void SetVec(string name, float x, float y, float z)
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();
			OpenGL.glUniform3f(param, x, y, z);
			OpenGL.CheckGLError();
		}

		public void SetVec(string name, float x, float y, float z, float w)
		{
			VerifyThreadAffinity();
			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();
			OpenGL.glUniform4f(param, x, y, z, w);
			OpenGL.CheckGLError();
		}

		public void SetVecArray(string name, float[] vec, int vecLength, int count)
		{
			VerifyThreadAffinity();

			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();

			unsafe
			{
				fixed (float* pVec = vec)
				{
					var ptr = new IntPtr(pVec);
					switch (vecLength)
					{
						case 1: OpenGL.glUniform1fv(param, count, ptr); break;
						case 2: OpenGL.glUniform2fv(param, count, ptr); break;
						case 3: OpenGL.glUniform3fv(param, count, ptr); break;
						case 4: OpenGL.glUniform4fv(param, count, ptr); break;
						default: throw new InvalidDataException("Invalid vector length");
					}
				}
			}

			OpenGL.CheckGLError();
		}

		public void SetMatrix(string name, float[] mtx, int count = 1)
		{
			VerifyThreadAffinity();
			if (count == 1 && mtx.Length != 16)
				throw new InvalidDataException("Invalid 4x4 matrix");

			OpenGL.glUseProgram(program);
			OpenGL.CheckGLError();
			var param = OpenGL.glGetUniformLocation(program, name);
			OpenGL.CheckGLError();

			unsafe
			{
				fixed (float* pMtx = mtx)
					OpenGL.glUniformMatrix4fv(param, count, false, new IntPtr(pMtx));
			}

			OpenGL.CheckGLError();
		}

		public void LayoutAttributes()
		{
			foreach (var attribute in bindings.Attributes)
			{
				switch (attribute.Type)
				{
					case AttributeType.Float:
						OpenGL.glVertexAttribPointer(attribute.Index, attribute.Components, OpenGL.GL_FLOAT, false, bindings.Stride, new IntPtr(attribute.Offset));
						OpenGL.CheckGLError();
						break;
					case AttributeType.Int32:
						OpenGL.glVertexAttribIPointer(attribute.Index, attribute.Components, OpenGL.GL_INT, bindings.Stride, new IntPtr(attribute.Offset));
						OpenGL.CheckGLError();
						break;
					case AttributeType.UInt32:
						OpenGL.glVertexAttribIPointer(attribute.Index, attribute.Components, OpenGL.GL_UNSIGNED_INT, bindings.Stride, new IntPtr(attribute.Offset));
						OpenGL.CheckGLError();
						break;
					default:
						throw new Exception("Not Valide AttributeType");
				}

				OpenGL.glEnableVertexAttribArray(attribute.Index);
				OpenGL.CheckGLError();
			}
		}

		public void LayoutInstanceArray()
		{
			if (bindings.Instanced == false)
				throw new Exception("this shader is not instanced");

			foreach (var attribute in bindings.InstanceAttributes)
			{
				//OpenGL.glVertexAttribPointer(attribute.Index, attribute.Components, OpenGL.GL_FLOAT, false, bindings.InstanceStrde, new IntPtr(attribute.Offset));
				//OpenGL.CheckGLError();
				switch (attribute.Type)
				{
					case AttributeType.Float:
						OpenGL.glVertexAttribPointer(attribute.Index, attribute.Components, OpenGL.GL_FLOAT, false, bindings.InstanceStrde, new IntPtr(attribute.Offset));
						OpenGL.CheckGLError();
						break;
					case AttributeType.Int32:
						OpenGL.glVertexAttribIPointer(attribute.Index, attribute.Components, OpenGL.GL_INT, bindings.InstanceStrde, new IntPtr(attribute.Offset));
						OpenGL.CheckGLError();
						break;
					case AttributeType.UInt32:
						OpenGL.glVertexAttribIPointer(attribute.Index, attribute.Components, OpenGL.GL_UNSIGNED_INT, bindings.InstanceStrde, new IntPtr(attribute.Offset));
						OpenGL.CheckGLError();
						break;
					default:
						throw new Exception("Not Valide AttributeType");
				}

				OpenGL.glEnableVertexAttribArray(attribute.Index);
				OpenGL.CheckGLError();
				OpenGL.glVertexAttribDivisor(attribute.Index, 1);
				OpenGL.CheckGLError();
			}
		}

		public void SetCommonParaments(World3DRenderer w3dr, bool sunCamera)
		{
			bindings.SetCommonParaments(this, w3dr, sunCamera);
		}

	}
}
