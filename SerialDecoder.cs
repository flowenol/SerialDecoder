using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using LabNation.Interfaces;

namespace LabNation.Decoders
{
	[Export(typeof(IDecoder))]
	public class SerialDecoder : IDecoder
	{
		public const int BAUD_9600 = 9600;
		public const double BAUD_9600_LENGTH = 0.000104;

		public const int BAUD_19200 = 19200;
		public const double BAUD_19200_LENGTH = 0.000052;

		public const int BAUD_38400 = 38400;
		public const double BAUD_38400_LENGTH = 0.000026;

		public const int BAUD_57600 = 57600;
		public const double BAUD_57600_LENGTH = 0.000017;

		public const int BAUD_115200 = 115200;
		public const double BAUD_115200_LENGTH = 0.0000085;

		public const String PARITY_NONE = "NONE";
		public const String PARITY_ODD = "ODD";
		public const String PARITY_EVEN = "EVEN";

		public class BitSet
		{

			private bool value;
			private int count;
			private int startIndex;

			public bool Value {
				get {
					return this.value;
				}
				set {
					this.value = value;
				}
			}

			public int Count {
				get {
					return this.count;
				}
				set {
					this.count = value;
				}
			}

			public int StartIndex {
				get {
					return this.startIndex;
				}
				set {
					this.startIndex = value;
				}
			}

			public BitSet(bool value, int startIndex) {
				this.Value = value;
				this.StartIndex = startIndex;
			}
				
			public void Increment() {
				Count++;
			}
		}

		public DecoderDescription Description
		{
			get
			{
				return new DecoderDescription()
				{
					Name = "Serial Decoder",
					ShortName = "UART",
					Author = "P. Jaczewski",
					VersionMajor = 0,
					VersionMinor = 1,
					Description = "A simple decoder to decode the serial (UART) communication",
					InputWaveformTypes = new Dictionary<string, Type>() 
					{
						{ "UART", typeof(bool)}
					},
					Parameters = new DecoderParameter[]
					{
						new DecoderParamaterInts("BAUD", new int[] {BAUD_9600, BAUD_19200, BAUD_38400, BAUD_57600, BAUD_115200}, 
							"baud", BAUD_9600, "Baud rate."),
						new DecoderParamaterInts("BITS", new int[] {5, 6, 7, 8}, "bits", 8, "Number of bits."),
						new DecoderParamaterInts("STOP", new int[] {1, 2}, "stop", 1, "Number of stop bits."),
						new DecoderParamaterStrings("PARITY", new String[] {PARITY_NONE, PARITY_EVEN, PARITY_ODD}, PARITY_NONE, "Parity type.")
					}
				};
			}
		}

		public DecoderOutput[] Decode(Dictionary<string, Array> inputWaveforms, Dictionary<string, object> parameters, double samplePeriod)
		{
			//name input waveforms for easier usage
			bool[] UART = (bool[])inputWaveforms["UART"];

			//initialize output structure
			List<DecoderOutput> decoderOutputList = new List<DecoderOutput>();

			// determine number of bits
			int numberOfBits = (int) parameters ["BITS"];

			// determine number of stop bits
			int numberOfStopBits = (int) parameters ["STOP"];

			// determine parity type
			String parityType = (String) parameters ["PARITY"];

			//determine bit length according to desired baud rate
			double bitLength = 0;
			switch ((int) parameters ["BAUD"]) 
			{
			case BAUD_115200:
				{
					bitLength = BAUD_115200_LENGTH;
					break;
				}
			case BAUD_57600:
				{
					bitLength = BAUD_57600_LENGTH;
					break;
				}
			case BAUD_38400:
				{
					bitLength = BAUD_38400_LENGTH;
					break;
				}
			case BAUD_19200:
				{
					bitLength = BAUD_19200_LENGTH;
					break;
				}
			case BAUD_9600:
				{
					bitLength = BAUD_9600_LENGTH;
					break;
				}
			}

			int samplesPerBit = (int)(bitLength / samplePeriod);

			//begin decode for sample set
			LinkedList<BitSet> values = new LinkedList<BitSet> ();
			if (UART != null && UART.Length > 0) {

				//find bit sets
				for (int i = 0; i < UART.Length; i++) {
					if (values.Count == 0 || !values.Last.Value.Value.Equals (UART [i])) {
						values.AddLast (new BitSet (UART [i], i));
					} 

					values.Last.Value.Increment ();
				}

				DecoderOutput frameStartDecoderOutput = null;
				bool frameStarted = false;
				bool[] decodedBits = new bool[8]{ false, false, false, false, false, false, false, false };
				int decodedBitsCount = 0;
				int dataStartIndex = 0;
				int dataStopIndex = 0;
				DecoderOutput parityDecoderOutput = null;
				bool parityBitFound = false;

				// decode bytes
				foreach (BitSet value in values) {
					int bits = (int)Math.Round ((double)value.Count / samplesPerBit, MidpointRounding.ToEven);

					if (!frameStarted) {
						
						// check for start bit
						if (!value.Value) {
							if (bits > 0) {
								frameStarted = true;
								frameStartDecoderOutput = new DecoderOutputEvent(value.StartIndex, value.StartIndex + samplesPerBit, 
									DecoderOutputColor.Orange, "B");
							}

							// decode data if available
							if (bits - 1 > 0) {

								dataStartIndex = dataStopIndex = value.StartIndex + samplesPerBit;

								for (int i = 0; i < bits - 1 && i < numberOfBits - 1; i++) {
									decodedBits [i] = value.Value;
									decodedBitsCount = i + 1;
								} 
							}
						}
					} else {
						// decode data
						int processedBitCount = 0;
						if (decodedBitsCount + bits <= numberOfBits) {

							if (decodedBitsCount == 0) {
								dataStartIndex = dataStopIndex = value.StartIndex;
							}

							for (int i = decodedBitsCount; i < decodedBitsCount + bits; i++) {
								decodedBits [i] = value.Value;
								processedBitCount++;
							}
							decodedBitsCount += processedBitCount;
							dataStopIndex += processedBitCount * samplesPerBit;
						} else if (decodedBitsCount + bits > numberOfBits) {

							// decode remaining data if available
							for (int i = decodedBitsCount; i < numberOfBits; i++) {
								decodedBits [i] = value.Value;
								processedBitCount++;
							}
							decodedBitsCount += processedBitCount;
							dataStopIndex += processedBitCount * samplesPerBit;

							// omit parity check if set to NONE
							if (parityType == PARITY_NONE) {
								parityBitFound = true;
							}

							// check parity
							if (!parityBitFound && bits - processedBitCount > 0) {
								bool parity = checkParity (decodedBits);

								if ((parityType == PARITY_EVEN && value.Value == parity) ||
								    (parityType == PARITY_ODD && value.Value == !parity)) {
									parityBitFound = true;
									parityDecoderOutput = new DecoderOutputEvent (
										value.StartIndex + processedBitCount * samplesPerBit, 
										value.StartIndex + (processedBitCount + 1) * samplesPerBit, 
										DecoderOutputColor.Green, "P");
									processedBitCount++;
								} else {
									// if parity is wrong, then clear context values and proceed to next bit set
									frameStarted = false;
									frameStartDecoderOutput = null;
									parityBitFound = false;
									parityDecoderOutput = null;
									decodedBitsCount = 0;
									decodedBits = new bool[8]{ false, false, false, false, false, false, false, false };
									continue;
								}
							}

							// check for stop bits
							if (parityBitFound && bits - processedBitCount > 0) {
								if (value.Value == true && bits - processedBitCount >= numberOfStopBits) {
									byte decodedByte = decodeByte (decodedBits);
									decoderOutputList.Add (frameStartDecoderOutput);
									decoderOutputList.Add (new DecoderOutputValue<byte> (dataStartIndex, dataStopIndex, 
										DecoderOutputColor.DarkBlue, decodedByte, "data"));

									if (parityDecoderOutput != null) {
												decoderOutputList.Add (parityDecoderOutput);
									}
									decoderOutputList.Add (new DecoderOutputEvent (
										value.StartIndex + processedBitCount * samplesPerBit, 
										value.StartIndex + (processedBitCount + numberOfStopBits) * samplesPerBit, 
										DecoderOutputColor.Red, "E"));
								}

								// clear context values and proceed to next bit set
								frameStarted = false;
								frameStartDecoderOutput = null;
								parityBitFound = false;
								parityDecoderOutput = null;
								decodedBitsCount = 0;
								decodedBits = new bool[8]{ false, false, false, false, false, false, false, false };
								continue;
							} 
						}
					}
				}
			}

			return decoderOutputList.ToArray();
		}

		// decodes byte from bool array
		private static byte decodeByte(bool[] bits) {
			byte decodedByte = 0x00;
			for (byte i = 0; i < 8; i++) {
				if (bits [i]) {
					decodedByte = (byte)(decodedByte | 0x01 << i); 
				}
			}

			return decodedByte;
		}

		// checks even parity, negate for odd parity
		private static bool checkParity(bool[] bits) {
			int ones = 0;
			for (byte i = 0; i < 8; i++) {
				if (bits[i]) ones++;
			}

			return ones % 2 != 0;
		}
	}
}