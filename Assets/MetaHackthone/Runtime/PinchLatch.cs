namespace PhysicalAssembly
{
    // Distance hysteresis also works when a provider supplies joints but no pinch flag.
    public sealed class PinchLatch
    {
        public bool IsPinching { get; private set; }
        bool armed;
        public bool Update(bool valid,float normalizedTipDistance,bool systemPinch)
        {
            if(!valid) { Reset(); return false; }
            if(!armed)
            {
                if(normalizedTipDistance>=.045f) armed=true;
                return false;
            }
            if(IsPinching) IsPinching=normalizedTipDistance<.045f;
            else IsPinching=normalizedTipDistance<=.028f || (systemPinch && normalizedTipDistance<.04f);
            return IsPinching;
        }
        public void Reset() { armed=false; IsPinching=false; }
    }
}
