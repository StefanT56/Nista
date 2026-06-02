using Common;
using Modbus.FunctionParameters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace Modbus.ModbusFunctions
{
    /// <summary>
    /// Class containing logic for parsing and packing modbus read coil functions/requests.
    /// </summary>
    public class ReadCoilsFunction : ModbusFunction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadCoilsFunction"/> class.
        /// </summary>
        /// <param name="commandParameters">The modbus command parameters.</param>
		public ReadCoilsFunction(ModbusCommandParameters commandParameters) : base(commandParameters)
        {
            CheckArguments(MethodBase.GetCurrentMethod(), typeof(ModbusReadCommandParameters));
        }

        /// <inheritdoc/>
        public override byte[] PackRequest()
        {
            byte[] request = new byte[12];

            ModbusReadCommandParameters parameters = (ModbusReadCommandParameters)CommandParameters;

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.TransactionId)), 0, request, 0, 2);

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.ProtocolId)), 0, request, 2, 2);

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.Length)), 0, request, 4, 2);

            request[6] = parameters.UnitId;
            request[7] = parameters.FunctionCode;

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.StartAddress)), 0, request, 8, 2);

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.Quantity)), 0, request, 10, 2);

            return request;
        }

        /// <inheritdoc />
        public override Dictionary<Tuple<PointType, ushort>, ushort> ParseResponse(byte[] response)
        {
            Dictionary<Tuple<PointType, ushort>, ushort> retVal = new Dictionary<Tuple<PointType, ushort>, ushort>();

            ModbusReadCommandParameters parameters = (ModbusReadCommandParameters)CommandParameters;

            if (response[7] == parameters.FunctionCode + 0x80)
            {
                HandeException(response[8]);
            }

            byte byteCount = response[8];

            for (int i = 0; i < parameters.Quantity; i++)
            {
                int byteIndex = 9 + (i / 8);
                int bitIndex = i % 8;

                ushort value = (ushort)((response[byteIndex] >> bitIndex) & 1);
                ushort address = (ushort)(parameters.StartAddress + i);

                retVal.Add(new Tuple<PointType, ushort>(PointType.DIGITAL_OUTPUT, address), value);
            }

            return retVal;
        }
    }
}