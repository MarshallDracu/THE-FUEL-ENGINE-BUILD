// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using LibreLancer.Render;
using LibreLancer.Thorn;
using LibreLancer.World;

namespace LibreLancer.Thn
{
    public abstract class ThnEvent
    {
        public float Duration;
        public float Time;
        public string[] Targets;
        public EventTypes Type;
        public ParameterCurve ParamCurve;

        protected ThnEvent()
        {
        }

        public virtual void Run(ThnScriptInstance instance)
        {
            FLLog.Error("Thn", $"({Time}): Unimplemented event: {Type}");
        }

        public float GetT(double intime)
        {
            if (double.IsNaN(intime) || double.IsInfinity(intime)) intime = 0;
            if (Duration <= 0f) return 1f;
            if (intime < 0) intime = 0;
            if (intime > Duration) intime = Duration;

            float t;
            if (ParamCurve != null)
            {
                t = ParamCurve.GetValue((float)intime, Duration);
                if (float.IsNaN(t) || float.IsInfinity(t)) t = 0f;
                if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            }
            else
            {
                t = (float)(intime / Duration);
                if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            }
            return t;
        }

        // zachowaj kompatybilność z istniejącymi wywołaniami
        public float GetT(float intime) => GetT((double)intime);

        protected ThnEvent(ThornTable table)
        {
            Time = (float)table[1];
            Type = ThnTypes.Convert<EventTypes>(table[2]);

            // Właściwości zdarzenia
            if (GetProps(table, out var props))
            {
                // duration
                GetValue(props, "duration", out Duration);

                // param_curve
                if (GetValue(props, "param_curve", out ThornTable paramCurveTable))
                {
                    ParamCurve = new ParameterCurve(paramCurveTable);

                    // pcurve_period (opcjonalny)
                    if (GetValue(props, "pcurve_period", out float period))
                    {
                        // sanity-check + fallback
                        if (float.IsNaN(period) || float.IsInfinity(period) || period <= 0f)
                            period = (Duration > 0f) ? Duration : 1f;

                        ParamCurve.Period = period;
                    }
                    else
                    {
                        // jeśli nie podano, można ustawić sensowny domyślny okres
                        if (ParamCurve.Period <= 0f)
                            ParamCurve.Period = (Duration > 0f) ? Duration : 1f;
                    }
                }
            }

            // cele (targets)
            var targetTable = (ThornTable)table[3];
            Targets = new string[targetTable.Length];
            for (int i = 1; i <= Targets.Length; i++)
                Targets[i - 1] = (string)targetTable[i];
        }

        protected static bool GetValue<T>(ThornTable table, string key, out T result, T def = default(T))
        {
            result = default;
            if (table.TryGetValue(key, out var tmp))
            {
                result = ThnTypes.Convert<T>(tmp);
                return true;
            }
            return false;
        }

        protected static bool GetProps(ThornTable table, out ThornTable props)
        {
            props = null;
            if (table.Length >= 4)
            {
                props = (ThornTable)table[4];
                return true;
            }
            return false;
        }

        protected static IRenderHardpoint GetHardpoint(GameObject obj, string hp)
        {
            if (obj.RenderComponent is CharacterRenderer ch)
            {
                ch.Skeleton.Hardpoints.TryGetValue(hp, out var h);
                return h;
            }
            return obj.GetHardpoint(hp);
        }
    }
}
