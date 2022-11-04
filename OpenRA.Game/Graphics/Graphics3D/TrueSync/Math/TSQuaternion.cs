﻿/* Copyright (C) <2009-2011> <Thorben Linneweber, Jitter Physics>
* 
*  This software is provided 'as-is', without any express or implied
*  warranty.  In no event will the authors be held liable for any damages
*  arising from the use of this software.
*
*  Permission is granted to anyone to use this software for any purpose,
*  including commercial applications, and to alter it and redistribute it
*  freely, subject to the following restrictions:
*
*  1. The origin of this software must not be misrepresented; you must not
*      claim that you wrote the original software. If you use this software
*      in a product, an acknowledgment in the product documentation would be
*      appreciated but is not required.
*  2. Altered source versions must be plainly marked as such, and must not be
*      misrepresented as being the original software.
*  3. This notice may not be removed or altered from any source distribution. 
*/

using System;
using GlmSharp;

namespace TrueSync
{

	/// <summary>
	/// A Quaternion representing an orientation.
	/// </summary>
	[Serializable]
	public struct TSQuaternion
	{

		/// <summary>The X component of the quaternion.</summary>
		public FP x;
		/// <summary>The Y component of the quaternion.</summary>
		public FP y;
		/// <summary>The Z component of the quaternion.</summary>
		public FP z;
		/// <summary>The W component of the quaternion.</summary>
		public FP w;

		public static readonly TSQuaternion identity;

		static TSQuaternion()
		{
			identity = new TSQuaternion(0, 0, 0, 1);
		}

		/// <summary>
		/// Initializes a new instance of the JQuaternion structure.
		/// </summary>
		/// <param name="x">The X component of the quaternion.</param>
		/// <param name="y">The Y component of the quaternion.</param>
		/// <param name="z">The Z component of the quaternion.</param>
		/// <param name="w">The W component of the quaternion.</param>
		public TSQuaternion(FP x, FP y, FP z, FP w)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.w = w;
		}

		public void Set(FP new_x, FP new_y, FP new_z, FP new_w)
		{
			this.x = new_x;
			this.y = new_y;
			this.z = new_z;
			this.w = new_w;
		}

		public void SetFromToRotation(TSVector fromDirection, TSVector toDirection)
		{
			TSQuaternion targetRotation = TSQuaternion.FromToRotation(fromDirection, toDirection);
			this.Set(targetRotation.x, targetRotation.y, targetRotation.z, targetRotation.w);
		}

		public TSVector eulerAngles
		{
			get
			{
				TSVector result = new TSVector();

				FP ysqr = y * y;
				FP t0 = -2.0f * (ysqr + z * z) + 1.0f;
				FP t1 = +2.0f * (x * y - w * z);
				FP t2 = -2.0f * (x * z + w * y);
				FP t3 = +2.0f * (y * z - w * x);
				FP t4 = -2.0f * (x * x + ysqr) + 1.0f;

				t2 = t2 > 1.0f ? 1.0f : t2;
				t2 = t2 < -1.0f ? -1.0f : t2;

				result.x = FP.Atan2(t3, t4) * FP.Rad2Deg;
				result.y = FP.Asin(t2) * FP.Rad2Deg;
				result.z = FP.Atan2(t1, t0) * FP.Rad2Deg;

				return result * -1;
			}
		}

		public TSVector eulerRad
		{
			get
			{
				TSVector result = new TSVector();

				FP ysqr = y * y;
				FP t0 = -2.0f * (ysqr + z * z) + 1.0f;
				FP t1 = +2.0f * (x * y - w * z);
				FP t2 = -2.0f * (x * z + w * y);
				FP t3 = +2.0f * (y * z - w * x);
				FP t4 = -2.0f * (x * x + ysqr) + 1.0f;

				t2 = t2 > 1.0f ? 1.0f : t2;
				t2 = t2 < -1.0f ? -1.0f : t2;

				result.x = FP.Atan2(t3, t4);
				result.y = FP.Asin(t2);
				result.z = FP.Atan2(t1, t0);

				return result * -1;
			}
		}

		public static FP Angle(TSQuaternion a, TSQuaternion b)
		{
			TSQuaternion aInv = TSQuaternion.Inverse(a);
			TSQuaternion f = (b * aInv).Normalize();

			FP angle = FP.Acos(f.w) * 2 * FP.Rad2Deg;

			if (angle > 180)
			{
				angle = 360 - angle;
			}

			return angle;
		}

		/// <summary>
		/// Quaternions are added.
		/// </summary>
		/// <param name="quaternion1">The first quaternion.</param>
		/// <param name="quaternion2">The second quaternion.</param>
		/// <returns>The sum of both quaternions.</returns>
		#region public static JQuaternion Add(JQuaternion quaternion1, JQuaternion quaternion2)
		public static TSQuaternion Add(TSQuaternion quaternion1, TSQuaternion quaternion2)
		{
			TSQuaternion result;
			TSQuaternion.Add(ref quaternion1, ref quaternion2, out result);
			return result;
		}

		public static TSQuaternion LookRotation(TSVector forward)
		{
			return CreateFromMatrix(TSMatrix.LookAt(forward, TSVector.up));
		}

		public static TSQuaternion LookRotation(TSVector forward, TSVector upwards)
		{
			return CreateFromMatrix(TSMatrix.LookAt(forward, upwards));
		}

		public static TSQuaternion Slerp(in TSQuaternion from, TSQuaternion to, FP t)
		{
			t = TSMath.Clamp(t, 0, 1);

			// notice that some time the abs value can be a littile biger than one
			FP dot = Dot(from, to);

			if (dot < 0.0f)
			{
				to = Multiply(to, -1);
				dot = -dot;
			}

			if (dot < 1)
			{
				FP halfTheta = FP.Acos(dot);

				return (Multiply(Multiply(from, FP.Sin((1 - t) * halfTheta)) + Multiply(to, FP.Sin(t * halfTheta)), 1 / FP.Sin(halfTheta))).Normalize();
			}
			else
			{
				return Lerp(from, to, t);
			}
		}

		public static TSQuaternion FastSlerp(TSQuaternion from, TSQuaternion to, FP t)
		{
			//t = TSMath.Clamp(t, 0, 1);

			// notice that some time the abs value can be a littile biger than one
			FP dot = Dot(from, to);

			if (dot < 0.0f)
			{
				FastMultiply(ref to, -1);
				dot = -dot;
			}

			if (dot < 1)
			{
				FP halfTheta = FP.Acos(dot);
				FastMultiply(ref from, FP.Sin((1 - t) * halfTheta));
				FastMultiply(ref to, FP.Sin(t * halfTheta));
				from += to;
				FastMultiply(ref from, 1 / FP.Sin(halfTheta));
				return from.Normalize();
			}
			else
			{
				return LerpUnclamped(from, to, t);
			}
		}

		public static TSQuaternion RotateTowards(in TSQuaternion from, TSQuaternion to, FP maxDegreesDelta)
		{
			FP dot = Dot(from, to);

			if (dot < 0.0f)
			{
				to = Multiply(to, -1);
				dot = -dot;
			}

			if (dot < 1)
			{

				FP halfTheta = FP.Acos(dot);
				FP theta = halfTheta * 2;

				maxDegreesDelta *= FP.Deg2Rad;

				if (maxDegreesDelta >= theta)
				{
					return to;
				}

				maxDegreesDelta /= theta;

				return Multiply(Multiply(from, FP.Sin((1 - maxDegreesDelta) * halfTheta)) + Multiply(to, FP.Sin(maxDegreesDelta * halfTheta)), 1 / FP.Sin(halfTheta));
			}
			else
			{
				return to;
			}
		}

		public static TSQuaternion Euler(FP x, FP y, FP z)
		{
			x *= FP.Deg2Rad;
			y *= FP.Deg2Rad;
			z *= FP.Deg2Rad;

			TSQuaternion rotation;
			TSQuaternion.CreateFromYawPitchRoll(y, x, z, out rotation);

			return rotation;
		}

		public static TSQuaternion EulerRad(FP pitch, FP yaw, FP roll)
		{
			TSQuaternion rotation;
			TSQuaternion.CreateFromYawPitchRoll(yaw, pitch, roll, out rotation);

			return rotation;
		}

		public static TSQuaternion Euler(TSVector eulerAngles)
		{
			return Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z);
		}

		public TSQuaternion InvPitch()
		{
			TSVector eulerAngles = this.eulerAngles;
			return Euler(-eulerAngles.x, eulerAngles.y, eulerAngles.z);
		}

		public static TSQuaternion AngleAxis(FP angle, TSVector axis)
		{
			axis = axis * FP.Deg2Rad;
			axis.Normalize();

			FP halfAngle = angle * FP.Deg2Rad * FP.Half;

			TSQuaternion rotation;
			FP sin = FP.Sin(halfAngle);

			rotation.x = axis.x * sin;
			rotation.y = axis.y * sin;
			rotation.z = axis.z * sin;
			rotation.w = FP.Cos(halfAngle);

			return rotation;
		}

		public static void CreateFromYawPitchRoll(FP yaw, FP pitch, FP roll, out TSQuaternion result)
		{
			FP num9 = roll * FP.Half;
			FP num6 = FP.Sin(num9);
			FP num5 = FP.Cos(num9);
			FP num8 = pitch * FP.Half;
			FP num4 = FP.Sin(num8);
			FP num3 = FP.Cos(num8);
			FP num7 = yaw * FP.Half;
			FP num2 = FP.Sin(num7);
			FP num = FP.Cos(num7);
			result.x = ((num * num4) * num5) + ((num2 * num3) * num6);
			result.y = ((num2 * num3) * num5) - ((num * num4) * num6);
			result.z = ((num * num3) * num6) - ((num2 * num4) * num5);
			result.w = ((num * num3) * num5) + ((num2 * num4) * num6);
		}

		/// <summary>
		/// Quaternions are added.
		/// </summary>
		/// <param name="quaternion1">The first quaternion.</param>
		/// <param name="quaternion2">The second quaternion.</param>
		/// <param name="result">The sum of both quaternions.</param>
		public static void Add(ref TSQuaternion quaternion1, ref TSQuaternion quaternion2, out TSQuaternion result)
		{
			result.x = quaternion1.x + quaternion2.x;
			result.y = quaternion1.y + quaternion2.y;
			result.z = quaternion1.z + quaternion2.z;
			result.w = quaternion1.w + quaternion2.w;
		}
		#endregion

		public static TSQuaternion Conjugate(TSQuaternion value)
		{
			TSQuaternion quaternion;
			quaternion.x = -value.x;
			quaternion.y = -value.y;
			quaternion.z = -value.z;
			quaternion.w = value.w;
			return quaternion;
		}

		public static FP Dot(in TSQuaternion a, in TSQuaternion b)
		{
			return a.w * b.w + a.x * b.x + a.y * b.y + a.z * b.z;
		}

		public static TSQuaternion Inverse(TSQuaternion rotation)
		{
			FP invNorm = FP.One / ((rotation.x * rotation.x) + (rotation.y * rotation.y) + (rotation.z * rotation.z) + (rotation.w * rotation.w));
			return TSQuaternion.Multiply(TSQuaternion.Conjugate(rotation), invNorm);
		}

		public static TSQuaternion FromToRotation(TSVector fromVector, TSVector toVector)
		{
			TSVector w = TSVector.Cross(fromVector, toVector);
			TSQuaternion q = new TSQuaternion(w.x, w.y, w.z, TSVector.Dot(fromVector, toVector));
			q.w += FP.Sqrt(fromVector.sqrMagnitude * toVector.sqrMagnitude);
			q.Normalize();

			return q;
		}

		public static TSQuaternion Lerp(in TSQuaternion a, in TSQuaternion b, FP t)
		{
			t = TSMath.Clamp(t, FP.Zero, FP.One);

			return LerpUnclamped(a, b, t);
		}

		public static TSQuaternion LerpUnclamped(in TSQuaternion a, in TSQuaternion b, in FP t)
		{
			TSQuaternion result = TSQuaternion.Multiply(a, (1 - t)) + TSQuaternion.Multiply(b, t);
			result.Normalize();

			return result;
		}

		/// <summary>
		/// Quaternions are subtracted.
		/// </summary>
		/// <param name="quaternion1">The first quaternion.</param>
		/// <param name="quaternion2">The second quaternion.</param>
		/// <returns>The difference of both quaternions.</returns>
		#region public static JQuaternion Subtract(JQuaternion quaternion1, JQuaternion quaternion2)
		public static TSQuaternion Subtract(TSQuaternion quaternion1, TSQuaternion quaternion2)
		{
			TSQuaternion result;
			TSQuaternion.Subtract(ref quaternion1, ref quaternion2, out result);
			return result;
		}

		/// <summary>
		/// Quaternions are subtracted.
		/// </summary>
		/// <param name="quaternion1">The first quaternion.</param>
		/// <param name="quaternion2">The second quaternion.</param>
		/// <param name="result">The difference of both quaternions.</param>
		public static void Subtract(ref TSQuaternion quaternion1, ref TSQuaternion quaternion2, out TSQuaternion result)
		{
			result.x = quaternion1.x - quaternion2.x;
			result.y = quaternion1.y - quaternion2.y;
			result.z = quaternion1.z - quaternion2.z;
			result.w = quaternion1.w - quaternion2.w;
		}
		#endregion

		/// <summary>
		/// Multiply two quaternions.
		/// </summary>
		/// <param name="quaternion1">The first quaternion.</param>
		/// <param name="quaternion2">The second quaternion.</param>
		/// <returns>The product of both quaternions.</returns>
		#region public static JQuaternion Multiply(JQuaternion quaternion1, JQuaternion quaternion2)
		public static TSQuaternion Multiply(TSQuaternion quaternion1, TSQuaternion quaternion2)
		{
			TSQuaternion result;
			TSQuaternion.Multiply(ref quaternion1, ref quaternion2, out result);
			return result;
		}

		/// <summary>
		/// Multiply two quaternions.
		/// </summary>
		/// <param name="quaternion1">The first quaternion.</param>
		/// <param name="quaternion2">The second quaternion.</param>
		/// <param name="result">The product of both quaternions.</param>
		public static void Multiply(ref TSQuaternion quaternion1, ref TSQuaternion quaternion2, out TSQuaternion result)
		{
			FP x = quaternion1.x;
			FP y = quaternion1.y;
			FP z = quaternion1.z;
			FP w = quaternion1.w;
			FP num4 = quaternion2.x;
			FP num3 = quaternion2.y;
			FP num2 = quaternion2.z;
			FP num = quaternion2.w;
			FP num12 = (y * num2) - (z * num3);
			FP num11 = (z * num4) - (x * num2);
			FP num10 = (x * num3) - (y * num4);
			FP num9 = ((x * num4) + (y * num3)) + (z * num2);
			result.x = ((x * num) + (num4 * w)) + num12;
			result.y = ((y * num) + (num3 * w)) + num11;
			result.z = ((z * num) + (num2 * w)) + num10;
			result.w = (w * num) - num9;
		}
		#endregion

		/// <summary>
		/// Scale a quaternion
		/// </summary>
		/// <param name="quaternion1">The quaternion to scale.</param>
		/// <param name="scaleFactor">Scale factor.</param>
		/// <returns>The scaled quaternion.</returns>
		#region public static JQuaternion Multiply(JQuaternion quaternion1, FP scaleFactor)
		public static TSQuaternion Multiply(TSQuaternion quaternion1, FP scaleFactor)
		{
			TSQuaternion result;
			TSQuaternion.Multiply(ref quaternion1, scaleFactor, out result);
			return result;
		}

		/// <summary>
		/// Scale a quaternion
		/// </summary>
		/// <param name="quaternion1">The quaternion to scale.</param>
		/// <param name="scaleFactor">Scale factor.</param>
		/// <param name="result">The scaled quaternion.</param>
		public static void Multiply(ref TSQuaternion quaternion1, FP scaleFactor, out TSQuaternion result)
		{
			result.x = quaternion1.x * scaleFactor;
			result.y = quaternion1.y * scaleFactor;
			result.z = quaternion1.z * scaleFactor;
			result.w = quaternion1.w * scaleFactor;
		}

		public static void FastMultiply(ref TSQuaternion quaternion1, FP scaleFactor)
		{
			quaternion1.x = FP.FastMul(quaternion1.x, scaleFactor);
			quaternion1.y = FP.FastMul(quaternion1.y, scaleFactor);
			quaternion1.z = FP.FastMul(quaternion1.z, scaleFactor);
			quaternion1.w = FP.FastMul(quaternion1.w, scaleFactor);
		}
		#endregion

		/// <summary>
		/// Sets the length of the quaternion to one.
		/// </summary>
		#region public void Normalize()
		public TSQuaternion Normalize()
		{
			FP num2 = (((this.x * this.x) + (this.y * this.y)) + (this.z * this.z)) + (this.w * this.w);
			FP num = 1 / (FP.Sqrt(num2));
			this.x *= num;
			this.y *= num;
			this.z *= num;
			this.w *= num;
			return this;
		}
		#endregion

		/// <summary>
		/// Creates a quaternion from a matrix.
		/// </summary>
		/// <param name="matrix">A matrix representing an orientation.</param>
		/// <returns>JQuaternion representing an orientation.</returns>
		#region public static JQuaternion CreateFromMatrix(JMatrix matrix)
		public static TSQuaternion CreateFromMatrix(TSMatrix matrix)
		{
			TSQuaternion result;
			TSQuaternion.CreateFromMatrix(ref matrix, out result);
			return result;
		}

		/// <summary>
		/// Creates a quaternion from a matrix.
		/// </summary>
		/// <param name="matrix">A matrix representing an orientation.</param>
		/// <param name="result">JQuaternion representing an orientation.</param>
		public static void CreateFromMatrix(ref TSMatrix matrix, out TSQuaternion result)
		{
			var fourXSquaredMinus1 = matrix.m00 - matrix.m11 - matrix.m22;
			var fourYSquaredMinus1 = matrix.m11 - matrix.m00 - matrix.m22;
			var fourZSquaredMinus1 = matrix.m22 - matrix.m00 - matrix.m11;
			var fourWSquaredMinus1 = matrix.m00 + matrix.m11 + matrix.m22;
			var biggestIndex = 0;
			var fourBiggestSquaredMinus1 = fourWSquaredMinus1;
			if (fourXSquaredMinus1 > fourBiggestSquaredMinus1)
			{
				fourBiggestSquaredMinus1 = fourXSquaredMinus1;
				biggestIndex = 1;
			}

			if (fourYSquaredMinus1 > fourBiggestSquaredMinus1)
			{
				fourBiggestSquaredMinus1 = fourYSquaredMinus1;
				biggestIndex = 2;
			}

			if (fourZSquaredMinus1 > fourBiggestSquaredMinus1)
			{
				fourBiggestSquaredMinus1 = fourZSquaredMinus1;
				biggestIndex = 3;
			}

			var biggestVal = TSMath.Sqrt(fourBiggestSquaredMinus1 + 1.0) * 0.5;
			var mult = 0.25 / biggestVal;
			switch (biggestIndex)
			{
				case 0:
					result.x = (matrix.m12 - matrix.m21) * mult;
					result.y = (matrix.m20 - matrix.m02) * mult;
					result.z = (matrix.m01 - matrix.m10) * mult;
					result.w = biggestVal;
					return;
				case 1:
					result.x = biggestVal;
					result.y = (matrix.m01 + matrix.m10) * mult;
					result.z = (matrix.m20 + matrix.m02) * mult;
					result.w = (matrix.m12 - matrix.m21) * mult;
					return;
				case 2:
					result.x = (matrix.m01 + matrix.m10) * mult;
					result.y = biggestVal;
					result.z = (matrix.m12 + matrix.m21) * mult;
					result.w = (matrix.m20 - matrix.m02) * mult;
					return;
				default:
					result.x = (matrix.m20 + matrix.m02) * mult;
					result.y = (matrix.m12 + matrix.m21) * mult;
					result.z = biggestVal;
					result.w = (matrix.m01 - matrix.m10) * mult;
					return;
			}

		//FP num8 = (matrix.m00 + matrix.m11) + matrix.m22;
		//if (num8 > FP.Zero)
		//{
		//	FP num = FP.Sqrt((num8 + FP.One));
		//	result.w = num * FP.Half;
		//	num = FP.Half / num;
		//	result.x = (matrix.m21 - matrix.m12) * num;
		//	result.y = (matrix.m02 - matrix.m20) * num;
		//	result.z = (matrix.m10 - matrix.m01) * num;
		//}
		//else if ((matrix.m00 >= matrix.m11) && (matrix.m00 >= matrix.m22))
		//{
		//	FP num7 = FP.Sqrt((((FP.One + matrix.m00) - matrix.m11) - matrix.m22));
		//	FP num4 = FP.Half / num7;
		//	result.x = FP.Half * num7;
		//	result.y = (matrix.m10 + matrix.m01) * num4;
		//	result.z = (matrix.m20 + matrix.m02) * num4;
		//	result.w = (matrix.m21 - matrix.m12) * num4;
		//}
		//else if (matrix.m11 > matrix.m22)
		//{
		//	FP num6 = FP.Sqrt((((FP.One + matrix.m11) - matrix.m00) - matrix.m22));
		//	FP num3 = FP.Half / num6;
		//	result.x = (matrix.m01 + matrix.m10) * num3;
		//	result.y = FP.Half * num6;
		//	result.z = (matrix.m12 + matrix.m21) * num3;
		//	result.w = (matrix.m02 - matrix.m20) * num3;
		//}
		//else
		//{
		//	FP num5 = FP.Sqrt((((FP.One + matrix.m22) - matrix.m00) - matrix.m11));
		//	FP num2 = FP.Half / num5;
		//	result.x = (matrix.m02 + matrix.m20) * num2;
		//	result.y = (matrix.m12 + matrix.m21) * num2;
		//	result.z = FP.Half * num5;
		//	result.w = (matrix.m10 - matrix.m01) * num2;
		//}
	}
		#endregion

		/// <summary>
		/// Multiply two quaternions.
		/// </summary>
		/// <param name="value1">The first quaternion.</param>
		/// <param name="value2">The second quaternion.</param>
		/// <returns>The product of both quaternions.</returns>
		#region public static FP operator *(JQuaternion value1, JQuaternion value2)
		public static TSQuaternion operator *(TSQuaternion value1, TSQuaternion value2)
		{
			TSQuaternion result;
			TSQuaternion.Multiply(ref value1, ref value2, out result);
			return result;
		}
		#endregion

		/// <summary>
		/// Add two quaternions.
		/// </summary>
		/// <param name="value1">The first quaternion.</param>
		/// <param name="value2">The second quaternion.</param>
		/// <returns>The sum of both quaternions.</returns>
		#region public static FP operator +(JQuaternion value1, JQuaternion value2)
		public static TSQuaternion operator +(TSQuaternion value1, TSQuaternion value2)
		{
			TSQuaternion result;
			TSQuaternion.Add(ref value1, ref value2, out result);
			return result;
		}
		#endregion

		/// <summary>
		/// Subtract two quaternions.
		/// </summary>
		/// <param name="value1">The first quaternion.</param>
		/// <param name="value2">The second quaternion.</param>
		/// <returns>The difference of both quaternions.</returns>
		#region public static FP operator -(JQuaternion value1, JQuaternion value2)
		public static TSQuaternion operator -(TSQuaternion value1, TSQuaternion value2)
		{
			TSQuaternion result;
			TSQuaternion.Subtract(ref value1, ref value2, out result);
			return result;
		}
		#endregion

		public static bool operator ==(TSQuaternion value1, TSQuaternion value2)
		{
			return value1.x == value2.x && value1.y == value2.y && value1.z == value2.z && value1.w == value2.w;
		}

		public static bool operator !=(TSQuaternion value1, TSQuaternion value2)
		{
			return !(value1 == value2);
		}

		/**
         *  @brief Rotates a {@link TSVector} by the {@link TSQuanternion}.
         **/
		public static TSVector operator *(TSQuaternion quat, TSVector vec)
		{
			FP num = quat.x * 2f;
			FP num2 = quat.y * 2f;
			FP num3 = quat.z * 2f;
			FP num4 = quat.x * num;
			FP num5 = quat.y * num2;
			FP num6 = quat.z * num3;
			FP num7 = quat.x * num2;
			FP num8 = quat.x * num3;
			FP num9 = quat.y * num3;
			FP num10 = quat.w * num;
			FP num11 = quat.w * num2;
			FP num12 = quat.w * num3;

			TSVector result;
			result.x = (1f - (num5 + num6)) * vec.x + (num7 - num12) * vec.y + (num8 + num11) * vec.z;
			result.y = (num7 + num12) * vec.x + (1f - (num4 + num6)) * vec.y + (num9 - num10) * vec.z;
			result.z = (num8 - num11) * vec.x + (num9 + num10) * vec.y + (1f - (num4 + num5)) * vec.z;

			return result;
		}

		public override string ToString()
		{
			return string.Format("({0:f1}, {1:f1}, {2:f1}, {3:f1})", x.AsFloat(), y.AsFloat(), z.AsFloat(), w.AsFloat());
		}

		public TSQuaternion(in quat quat)
			: this(quat.x, quat.y, quat.z, quat.w) { }

	}
}
