﻿
namespace Mozilla.NUniversalCharDet.Prober.DistributionAnalysis
{
    public class EUCJPDistributionAnalysis : JISDistributionAnalysis
    {
        ////////////////////////////////////////////////////////////////
        // constants
        ////////////////////////////////////////////////////////////////
        public static int HIGHBYTE_BEGIN = 0xA1;
        public static int HIGHBYTE_END = 0xFE;
        public static int LOWBYTE_BEGIN = 0xA1;
        public static int LOWBYTE_END = 0xFE;


        ////////////////////////////////////////////////////////////////
        // methods
        ////////////////////////////////////////////////////////////////
        public EUCJPDistributionAnalysis()
            : base()
        {
        }


        protected override int getOrder(byte[] buf, int offset)
        {
            int highbyte = buf[offset] & 0xFF;
            if (highbyte >= HIGHBYTE_BEGIN)
            {
                int lowbyte = buf[offset + 1] & 0xFF;
                return (94 * (highbyte - HIGHBYTE_BEGIN) + lowbyte - LOWBYTE_BEGIN);
            }
            else
            {
                return -1;
            }
        }
    }
}
