using System;
using Mozilla.NUniversalCharDet;
using Mozilla.NUniversalCharDet.Prober.ContextAnalysis;
using Mozilla.NUniversalCharDet.Prober.DistributionAnalysis;
using Mozilla.NUniversalCharDet.Prober.StateMachine;

namespace Mozilla.NUniversalCharDet.Prober
{
    /// <summary>
    /// Description of GB18030Prober.
    /// </summary>
    public class GB18030Prober : CharsetProber
    {
        ////////////////////////////////////////////////////////////////
        // fields
        ////////////////////////////////////////////////////////////////
        private CodingStateMachine codingSM;
        private ProbingState state;

        private GB2312DistributionAnalysis distributionAnalyzer;

        private byte[] lastChar;

        private static SMModel smModel = new GB18030SMModel();


        ////////////////////////////////////////////////////////////////
        // methods
        ////////////////////////////////////////////////////////////////
        public GB18030Prober()
            : base()
        {
            this.codingSM = new CodingStateMachine(smModel);
            this.distributionAnalyzer = new GB2312DistributionAnalysis();
            this.lastChar = new byte[2];
            reset();
        }

        public override string getCharSetName()
        {
            return Constants.CHARSET_GB18030;
        }


        public override float getConfidence()
        {
            float distribCf = this.distributionAnalyzer.getConfidence();

            return distribCf;
        }


        public override ProbingState getState()
        {
            return this.state;
        }

        public override ProbingState handleData(byte[] buf, int offset, int length)
        {
            int codingState;

            int maxPos = offset + length;
            for (int i = offset; i < maxPos; ++i)
            {
                codingState = codingSM.nextState(buf[i]);
                if (codingState == SMModel.ERROR)
                {
                    this.state = ProbingState.NOT_ME;
                    break;
                }
                if (codingState == SMModel.ITSME)
                {
                    this.state = ProbingState.FOUND_IT;
                    break;
                }
                if (codingState == SMModel.START)
                {
                    int charLen = this.codingSM.getCurrentCharLen();
                    if (i == offset)
                    {
                        this.lastChar[1] = buf[offset];
                        this.distributionAnalyzer.handleOneChar(this.lastChar, 0, charLen);
                    }
                    else
                    {
                        this.distributionAnalyzer.handleOneChar(buf, i - 1, charLen);
                    }
                }
            }

            this.lastChar[0] = buf[maxPos - 1];

            if (this.state == ProbingState.DETECTING)
            {
                if (this.distributionAnalyzer.gotEnoughData() && getConfidence() > SHORTCUT_THRESHOLD)
                {
                    this.state = ProbingState.FOUND_IT;
                }
            }

            return this.state;
        }

        public override void reset()
        {
            this.codingSM.reset();
            this.state = ProbingState.DETECTING;
            this.distributionAnalyzer.reset();
            Array.Clear(this.lastChar, 0, this.lastChar.Length);
            //java.util.Arrays.fill(this.lastChar, (byte)0);
        }

        public override void setOption()
        { }
    }

}
