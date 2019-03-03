
namespace Mozilla.NUniversalCharDet.Prober.StateMachine
{
    public abstract class SMModel
    {
        ////////////////////////////////////////////////////////////////
        // constants
        ////////////////////////////////////////////////////////////////
        public static int START = 0;
        public static int ERROR = 1;
        public static int ITSME = 2;


        ////////////////////////////////////////////////////////////////
        // fields
        ////////////////////////////////////////////////////////////////
        protected PkgInt classTable;
        protected int classFactor;
        protected PkgInt stateTable;
        protected int[] charLenTable;
        protected string name;


        ////////////////////////////////////////////////////////////////
        // methods
        ////////////////////////////////////////////////////////////////
        public SMModel(
                PkgInt classTable,
                int classFactor,
                PkgInt stateTable,
                int[] charLenTable,
                string name)
        {
            this.classTable = classTable;
            this.classFactor = classFactor;
            this.stateTable = stateTable;
            this.charLenTable = charLenTable;
            this.name = name;
        }

        public int getClass(byte b)
        {
            int c = b & 0xFF;
            return this.classTable.unpack(c);
        }

        public int getNextState(int cls, int currentState)
        {
            return this.stateTable.unpack(currentState * this.classFactor + cls);
        }

        public int getCharLen(int cls)
        {
            return this.charLenTable[cls];
        }

        public string getName()
        {
            return this.name;
        }
    }
}
