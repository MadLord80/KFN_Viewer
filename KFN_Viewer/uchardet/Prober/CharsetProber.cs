using System;

namespace Mozilla.NUniversalCharDet.Prober
{
	public abstract class CharsetProber
	{
		////////////////////////////////////////////////////////////////
		// constants
		////////////////////////////////////////////////////////////////
		public static float   SHORTCUT_THRESHOLD = 0.95f;
		public static int     ASCII_A = 0x61; // 'a'
		public static int     ASCII_Z = 0x7A; // 'z'
		public static int     ASCII_A_CAPITAL = 0x41; // 'A'
		public static int     ASCII_Z_CAPITAL = 0x5A; // 'Z'
		public static int     ASCII_LT = 0x3C; // '<'
		public static int     ASCII_GT = 0x3E; // '>'
		public static int     ASCII_SP = 0x20; // ' '

		
		////////////////////////////////////////////////////////////////
		// inner types
		////////////////////////////////////////////////////////////////
		public enum ProbingState
		{
			DETECTING,
			FOUND_IT,
			NOT_ME
		}

		
		////////////////////////////////////////////////////////////////
		// methods
		////////////////////////////////////////////////////////////////
		public CharsetProber()
		{}
		
		public abstract string getCharSetName();
		public abstract ProbingState handleData(byte[] buf, int offset, int length);
		public abstract ProbingState getState();
		public abstract void reset();
		public abstract float getConfidence();
		public abstract void setOption();

		// ByteBuffer.position() indicates number of bytes written.
		public ByteBuffer filterWithoutEnglishLetters(byte[] buf, int offset, int length)
		{
			ByteBuffer outByteBuffer = new ByteBuffer();
			
			bool meetMSB = false;
			byte c;

			int prevPtr = offset;
			int curPtr = offset;
			int maxPtr = offset + length;
			
			for (; curPtr<maxPtr; ++curPtr) {
				c = buf[curPtr];
				if (!isAscii(c)) {
					meetMSB = true;
				} else if (isAsciiSymbol(c)) {
					// current char is a symbol, most likely a punctuation.
					// we treat it as segment delimiter
					if (meetMSB && curPtr > prevPtr) {
						// this segment contains more than single symbol,
						// and it has upper ASCII, we need to keep it
						outByteBuffer.Put(buf, prevPtr, (curPtr-prevPtr));
						outByteBuffer.Put((byte)ASCII_SP);
						prevPtr = curPtr + 1;
						meetMSB = false;
					} else {
						// ignore current segment.
						// (either because it is just a symbol or just an English word)
						prevPtr = curPtr + 1;
					}
				}
			}
			
			if (meetMSB && curPtr > prevPtr) {
				outByteBuffer.Put(buf, prevPtr, (curPtr-prevPtr));
			}
			
			return outByteBuffer;
		}
		
		public ByteBuffer filterWithEnglishLetters(byte[] buf, int offset, int length)
		{
			ByteBuffer outByteBuffer = new ByteBuffer();
			
			bool isInTag = false;
			byte c;

			int prevPtr = offset;
			int curPtr = offset;
			int maxPtr = offset + length;
			
			for (; curPtr < maxPtr; ++curPtr) {
				c = buf[curPtr];
				
				if (c == ASCII_GT) {
					isInTag = false;
				} else if (c == ASCII_LT) {
					isInTag = true;
				}
				
				if (isAscii(c) && isAsciiSymbol(c)) {
					if (curPtr > prevPtr && !isInTag) {
						// Current segment contains more than just a symbol
						// and it is not inside a tag, keep it.
						outByteBuffer.Put(buf, prevPtr, (curPtr-prevPtr));
						outByteBuffer.Put((byte)ASCII_SP);
						prevPtr = curPtr + 1;
					} else {
						prevPtr = curPtr + 1;
					}
				}
			}
			
			// If the current segment contains more than just a symbol
			// and it is not inside a tag then keep it.
			if (!isInTag && curPtr > prevPtr) {
				outByteBuffer.Put(buf, prevPtr, (curPtr-prevPtr));
			}
			
			return outByteBuffer;
		}

		private bool isAscii(byte b)
		{
			return ((b & 0x80) == 0);
		}
		
		// b must be in ASCII code range (MSB can't be 1).
		private bool isAsciiSymbol(byte b)
		{
			int c = b & 0xFF;
			return ((c < ASCII_A_CAPITAL) ||
			        (c > ASCII_Z_CAPITAL && c < ASCII_A) ||
			        (c > ASCII_Z));
		}
	}

}
