using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using LabNation.Interfaces;

namespace LabNation.Decoders
{
	[Export(typeof(IDecoder))]
	public class SerialDecoder : IDecoder
	{

		public class SerialValue
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

			public SerialValue(bool value, int startIndex) {
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
						{ "TxD", typeof(bool)}
					},
					Parameters = new DecoderParameter[]
					{
						new DecoderParamaterInts("BAUD", new int[] {9600, 19200, 38400, 57600, 115200}, "baud", 9600, "Baud rate."),
					}
				};
			}
		}

		public DecoderOutput[] Decode(Dictionary<string, Array> inputWaveforms, Dictionary<string, object> parameters, double samplePeriod)
		{
			//name input waveforms for easier usage
			bool[] TXD = (bool[])inputWaveforms["TxD"];

			//initialize output structure
			List<DecoderOutput> decoderOutputList = new List<DecoderOutput>();

			//determine bit length according to desired baud rate
			double bitLength = 0;
			switch ((int) parameters ["BAUD"]) 
			{
				case 115200:
				{
					bitLength = 0.0000085;
					break;
				}
				case 57600:
				{
					bitLength = 0.000017;
					break;
				}
				case 38400:
				{
					bitLength = 0.000026;
					break;
				}
				case 19200:
				{
					bitLength = 0.000052;
					break;
				}
				case 9600:
				{
					bitLength = 0.000104;
					break;
				}
			}

			int samplesPerBit = (int)(bitLength / samplePeriod);

			//begin decode for sample set
			LinkedList<SerialValue> values = new LinkedList<SerialValue> ();
			if (TXD != null && TXD.Length > 0) {

				//find bits
				for (int i = 0; i < TXD.Length; i++) {
					if (values.Count == 0 || !values.Last.Value.Value.Equals (TXD [i])) {
						values.AddLast (new SerialValue (TXD [i], i));
					} 

					values.Last.Value.Increment ();
				}

				bool frameStarted = false;
				bool[] decodedBits = new bool[8]{false, false, false, false, false, false, false, false};
				int decodedBitsNumber = 0;
				int dataStartIndex = 0;

				// decode bytes
				foreach (SerialValue value in values) {
					int bits = (int)Math.Round ((double)value.Count / samplesPerBit, MidpointRounding.ToEven);

					if (!frameStarted) {
						// check for start bit
						if (!value.Value) {
							if (bits > 0) {
								frameStarted = true;
								decoderOutputList.Add(new DecoderOutputEvent(value.StartIndex, value.StartIndex + samplesPerBit, 
									DecoderOutputColor.Orange, "start"));
							}

							if (bits - 1 > 0) {

								dataStartIndex = value.StartIndex + samplesPerBit;

								for (int i = 0; i < bits - 1; i++) {
									decodedBits [i] = value.Value;
								}

								decodedBitsNumber = bits - 1; 
							}
						}
					} else {
						int bitCount = 0;
						if (decodedBitsNumber + bits <= 8) {

							if (decodedBitsNumber == 0) {
								dataStartIndex = value.StartIndex;
							}

							for (int i = decodedBitsNumber; i < decodedBitsNumber + bits; i++) {
								decodedBits [i] = value.Value;
								bitCount++;
							}
							decodedBitsNumber += bitCount;
						} else if (decodedBitsNumber + bits > 8) {
							for (int i = decodedBitsNumber; i < 8; i++) {
								decodedBits [i] = value.Value;
								bitCount++;
							}
							decodedBitsNumber += bitCount;

							// check for stop bit
							if (value.Value == true) {
								byte decodedByte = decodeByte (decodedBits);
								Console.WriteLine ("Decoded byte: " + String.Format("0x{0:X}", decodedByte));
								decoderOutputList.Add(new DecoderOutputValue<byte>(dataStartIndex, value.StartIndex, 
									DecoderOutputColor.DarkBlue, decodedByte, "data"));
								decoderOutputList.Add(new DecoderOutputEvent(value.StartIndex, value.StartIndex + samplesPerBit, 
									DecoderOutputColor.Orange, "stop"));
							}
							frameStarted = false;
							decodedBitsNumber = 0;
							continue;
						}
					}
				}
			}

			return decoderOutputList.ToArray();
		}

		private static byte decodeByte(bool[] bits) {
			byte decodedByte = 0x00;
			for (byte i = 0; i < 8; i++) {
				if (bits [i]) {
					decodedByte = (byte)(decodedByte | 0x01 << i); 
				}
			}

			return decodedByte;
		}
	}
}