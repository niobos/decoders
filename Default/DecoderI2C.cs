﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using LabNation.Interfaces;

namespace LabNation.Decoders
{
    [Export(typeof(IDecoder))]
    public class DecoderI2C : IDecoder
    {
        public DecoderDescription Description
        {
            get
            {
                return new DecoderDescription()
                {
                    Name = "I2C Decoder",
                    ShortName = "I2C",
                    Author = "LabNation",
                    VersionMajor = 0,
                    VersionMinor = 1,
                    Description = "I2C decoder, converting clock and data signal into Adress and Value bytes",
                    InputWaveformTypes = new Dictionary<string, Type>() 
                    {
                        { "SCL", typeof(bool)},
                        { "SDA", typeof(bool)}
                    },
                    Parameters = null
                };
            }
        }

        public DecoderOutput[] Decode(Dictionary<string, Array> inputWaveforms, Dictionary<string, object> parameters, double samplePeriod)
        {
            //name input waveforms for easier usage
            bool[] SCLK = (bool[])inputWaveforms["SCL"];
            bool[] SDIO = (bool[])inputWaveforms["SDA"];

            //initialize output structure
            List<DecoderOutput> decoderOutputList = new List<DecoderOutput>();

            //start of brute-force decoding
            bool i2cSequenceStarted = false;
            bool startEventFired = false;
            bool addressDecoded = false;
            int bitCounter = 0;
            int startIndex = 0;
            byte decodedByte = 0;
            bool read = false;

            for (int i = 1; i < SCLK.Length; i++)
            {
                bool clockRisingEdge = SCLK[i] && !SCLK[i - 1];
                bool dataRisingEdge = SDIO[i] && !SDIO[i - 1];
                bool dataFallingEdge = !SDIO[i] && SDIO[i - 1];                

                //check for start sequence
                if (dataFallingEdge && SCLK[i])
                {                    
                    i2cSequenceStarted = true;
                    startEventFired = true;
                    addressDecoded = false;
                    bitCounter = 8;
                    startIndex = i;
                }
                //Check for stop sequence
                else if (dataRisingEdge && SCLK[i])
                {
                    i2cSequenceStarted = false;
                    decoderOutputList.Add(new DecoderOutputEvent(startIndex, i, DecoderOutputColor.Orange, "P"));
                    startIndex = i;
                }

                //Decode byte
                if (i2cSequenceStarted && clockRisingEdge)
                {
                    //terminate start event
                    if (startEventFired)
                    {
                        startEventFired = false;
                        decoderOutputList.Add(new DecoderOutputEvent(startIndex, i, DecoderOutputColor.Green, "S"));
                        startIndex = i;
                    }

                    if (bitCounter == 8)
                    {
                        decodedByte = 0;
                        startIndex = i;
                    }

                    //for very first bit: check for R/W bit
                    if (!addressDecoded && (bitCounter == 1))
                        read = SDIO[i];
                    //for all other bits: accumulate value
                    else
                        if (bitCounter >= 1) //don't use ACK bit for accumulation
                            decodedByte = (byte)((decodedByte << 1) + (SDIO[i] ? 1 : 0));

                    if (bitCounter == 1)
                    {
                        if (!addressDecoded)
                        {
                            decoderOutputList.Add(new DecoderOutputValue<byte>(startIndex, i, DecoderOutputColor.DarkBlue, decodedByte, "Address"));
                            addressDecoded = true;
                        }
                        else
                        {
                            if (read)
                                decoderOutputList.Add(new DecoderOutputValue<byte>(startIndex, i, DecoderOutputColor.Purple, decodedByte, "Read"));
                            else
                                decoderOutputList.Add(new DecoderOutputValue<byte>(startIndex, i, DecoderOutputColor.DarkPurple, decodedByte, "Write"));
                        }
                        startIndex = i;
                    }

                    if (bitCounter == 0)
                    {
                        if (SDIO[i])
                            decoderOutputList.Add(new DecoderOutputEvent(startIndex, i, DecoderOutputColor.Orange, "NACK"));
                        else
                            decoderOutputList.Add(new DecoderOutputEvent(startIndex, i, DecoderOutputColor.Blue, "ACK"));
                        startIndex = i;
                        bitCounter = 8;
                    }
                    else
                        bitCounter--;
                }
            }

            return decoderOutputList.ToArray();
        }
    }
}
