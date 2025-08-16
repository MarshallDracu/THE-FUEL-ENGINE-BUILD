// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package
// Thanks to @Varon for his unity Catmull-Rom interpolation + explanations

using System;
using System.Collections.Generic;
using System.Numerics;
using LibreLancer.Thorn;

namespace LibreLancer.Thn
{
    /// <summary>
    /// Constant speed path using Catmull-Rom interpolation between points
    /// </summary>
    public class MotionPath
    {
        const int OPEN = 1;
        const int CLOSED = 0;
        static Dictionary<string, object> env = new Dictionary<string, object>()
        {
            { "OPEN", OPEN },
            { "CLOSED", CLOSED }
        };

        bool loop;
        public bool Closed
        {
            get
            {
                return loop;
            }
        }

        public bool HasOrientation { get; }

        Vector3 GetVec(object o)
        {
            var tab = (ThornTable)o;
            return new Vector3((float)tab[1], (float)tab[2], (float)tab[3]);
        }

        Quaternion GetQuat(object o)
        {
            var tab = (ThornTable)o;
            return new Quaternion((float)tab[2], (float)tab[3], (float)tab[4], (float)tab[1]);
        }

        private Vector3 startPoint, endPoint;
        private Quaternion startQuat, endQuat;
        private Quaternion[] curveQuats;
        private bool curve = false;
        public MotionPath(string pathdescriptor)
        {
            //Abuse the Lua runtime to parse the path descriptor for us
            var rt = new ThornRunner(env, null);
            var path = (ThornTable)(rt.DoString("path = {" + pathdescriptor + "}")["path"]);
            var type = (int)path[1];
            loop = type == CLOSED;
            //detect if orientations are present
            var orient = (ThornTable)path[3];
            HasOrientation = orient != null && orient.Length >= 4;
            var points = new List<Vector3>();
            var quaternions = new List<Quaternion>();
            //Construct path
            for (int i = 2; i <= path.Length; i++) {
                if (HasOrientation && i % 2 != 0)
                    quaternions.Add(GetQuat(path[i]));
                else
                    points.Add(GetVec(path[i]));
            }
            if (loop && HasOrientation && quaternions.Count > 0) {
                quaternions.Add(quaternions[0]); // domknij orientacje
            }
if (points.Count < 2)
    throw new Exception("Path does not have minimum two points");

curve = points.Count > 2;

startPoint = points[0];
endPoint   = points[points.Count - 1];

if (HasOrientation && quaternions.Count > 0) {
    startQuat = Quaternion.Normalize(quaternions[0]);
    endQuat   = Quaternion.Normalize(quaternions[quaternions.Count - 1]);
    if (curve) curveQuats = quaternions.ToArray();
}

if (curve) {
    BuildSegments(points);
}
        }

        private CubicPolynomial[] segments;
        private float[] segmentLengths;
        private float[] lengthPercents;
        void BuildSegments(List<Vector3> srcPoints)
        {
            var ps = new List<Vector3>(srcPoints.Count + 3);
            if (loop) {
                ps.Add(srcPoints[srcPoints.Count - 1]);
                ps.AddRange(srcPoints);
                ps.Add(srcPoints[0]);
                ps.Add(srcPoints[1]);
            }
            else
            {
                var first = (2 * srcPoints[0]) - srcPoints[1];
                var last = (2 * srcPoints[srcPoints.Count - 1]) - (srcPoints[srcPoints.Count - 2]);
                ps.Add(first);
                ps.AddRange(srcPoints);
                ps.Add(last);
            }
            var sg = new List<CubicPolynomial>();
            for (int i = 1; i < ps.Count - 2; i++)
            {
                sg.Add(CRCentripedal(ps[i - 1], ps[i], ps[i + 1], ps[i + 2]));
            }
            segments = sg.ToArray();
            segmentLengths = new float[segments.Length];
            lengthPercents = new float[segments.Length];
            float totalLength = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                var len = segments[i].ApproximateLength(100);
                segmentLengths[i] = len;
                totalLength += len;
            }
            if (totalLength <= float.Epsilon) {
                float even = 1f / segments.Length;
                for (int i = 0; i < segments.Length; i++)
                    lengthPercents[i] = even;
            } else {
                for (int i = 0; i < segments.Length; i++)
                    lengthPercents[i] = segmentLengths[i] / totalLength;
            }
        }
        public Vector3 GetPosition(float t)
        {
            if (t >= 1) return endPoint;
            if (t <= 0) return startPoint;
            if(!curve)
            {
                float dist = Vector3.Distance(startPoint, endPoint);
                var direction = (endPoint - startPoint).Normalized();
                return startPoint + (direction * (dist * t));
            }
            else
            {
                float seg0 = 0;
                for (int i = 0; i < segments.Length; i++)
                {
                    if (t <= seg0 + lengthPercents[i])
                    {
                        var t2 = (t - seg0) / lengthPercents[i];
                        return segments[i].ValueAt(t2);
                    }
                    else
                        seg0 += lengthPercents[i];
                }
                throw new Exception("Something has gone horribly wrong in MotionPath");
            }
        }

        public Vector3 GetDirection(float t, bool reverse = false)
        {
            const float STEP = 0.0025f;

            if (!curve)
            {
                var dir = (endPoint - startPoint);
                if (!reverse)
                    return dir.LengthSquared() > 0 ? dir.Normalized() : Vector3.UnitZ;
                else
                    return dir.LengthSquared() > 0 ? (-dir).Normalized() : Vector3.UnitZ;
            }

            t = MathHelper.Clamp(t, 0, 1);

            if (reverse)
            {
                var t0 = MathHelper.Clamp(t - STEP, 0, 1);
                var v  = GetPosition(t) - GetPosition(t0);
                return v.LengthSquared() > 0 ? v.Normalized() : GetDirection(t0, reverse);
            }
            else
            {
                var t1 = MathHelper.Clamp(t + STEP, 0, 1);
                var v  = GetPosition(t1) - GetPosition(t);
                return v.LengthSquared() > 0 ? v.Normalized() : GetDirection(t1, reverse);
            }
        }
        public Quaternion GetOrientation(float t)
        {
            if (!HasOrientation)
                throw new NotSupportedException();

            if (t <= 0) return startQuat;
            if (t >= 1) return endQuat;

            t = MathHelper.Clamp(t, 0, 1);

            if (curve && curveQuats != null && curveQuats.Length >= 2)
            {
                float seg0 = 0;
                for (int i = 0; i < segments.Length; i++)  {
                    float seg1 = seg0 + lengthPercents[i];
                    if (t <= seg1)
                    {
                        var denom = (lengthPercents[i] <= 0 ? 1e-6f : lengthPercents[i]);
                        var t2 = (t - seg0) / denom;
                        var s  = t2 * t2 * (3f - 2f * t2); // smoothstep
                        return Quaternion.Normalize(Quaternion.Slerp(curveQuats[i], curveQuats[i + 1], s));
                    }
                    seg0 = seg1;
                }
                throw new Exception("Something has gone horribly wrong in MotionPath");
            }
            else
            {
                var s = t * t * (3f - 2f * t); // smoothstep
                return Quaternion.Normalize(Quaternion.Slerp(startQuat, endQuat, s));
            }
        }

        static CubicPolynomial CRCentripedal(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float tau = 0.5f) {
            float dt0 = (float)Math.Pow((p0 - p1).LengthSquared(), tau);
            float dt1 = (float)Math.Pow((p1 - p2).LengthSquared(), tau);
            float dt2 = (float)Math.Pow((p2 - p3).LengthSquared(), tau);

            // safety check for repeated points
            if (dt1 < 1e-4f) {
                dt1 = 1.0f;
            }
            if (dt0 < 1e-4f) {
                dt0 = dt1;
            }
            if (dt2 < 1e-4f) {
                dt2 = dt1;
            }

            return CreateNonUniformCatmullRom(p0, p1, p2, p3, dt0, dt1, dt2);
        }

        static CubicPolynomial CreateNonUniformCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float dt0, float dt1, float dt2) {
            // compute tangents when parameterized in [t1,t2]
            Vector3 t1 = (p1 - p0)/dt0 - (p2 - p1)/(dt0 + dt1) + (p2 - p1)/dt1;
            Vector3 t2 = (p2 - p1)/dt1 - (p3 - p1)/(dt1 + dt2) + (p3 - p2)/dt2;

            // rescale tangents for parametrization in [0,1]
            t1 *= dt1;
            t2 *= dt1;

            return CubicPolynomial.Create(p1, t1, p2, t2);
        }

        struct CubicPolynomial {

            private readonly Vector3 c0;
            private readonly Vector3 c1;
            private readonly Vector3 c2;
            private readonly Vector3 c3;
            private CubicPolynomial(Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3) : this() {
                this.c0 = c0;
                this.c1 = c1;
                this.c2 = c2;
                this.c3 = c3;
            }

            public Vector3 ValueAt(float normalizedTime) {
                float t2 = normalizedTime*normalizedTime;
                float t3 = t2*normalizedTime;
                return c0 + c1*normalizedTime + c2*t2 + c3*t3;
            }

            public float ApproximateLength(float distance, float sampleRate = 10f) {
                // we do 100 samples per unit of straight line distance:
                float detail = distance*sampleRate;
                float increment = 1.0f/detail;
                float dist = 0f;
                Vector3 prev = ValueAt(0);
                float t = 0.0f;
                for (int i = 0; i < detail; i++) {
                    t += increment;
                    Vector3 to = ValueAt(t);
                    dist += Vector3.Distance(prev, to);
                    prev = to;
                }
                return dist;
            }
            public static CubicPolynomial Create(Vector3 x0, Vector3 t0, Vector3 x1, Vector3 t1) {
                Vector3 c0 = x0;
                Vector3 c1 = t0;
                Vector3 c2 = -3*x0 + 3*x1 - 2*t0 - t1;
                Vector3 c3 = 2*x0 - 2*x1 + t0 + t1;
                return new CubicPolynomial(c0, c1, c2, c3);
            }
        }
    }
}
