namespace Schedule4Net.Core
{
    public class ViolatorValues
    {
        public ViolatorValues()
        {
            SoftViolationsValue = 0;
            HardViolationsValue = 0;
        }

        public int HardViolationsValue { get; set; }
        public int SoftViolationsValue { get; set; }
    }
}
