using Common;
using Modbus.FunctionParameters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace Modbus.ModbusFunctions
{
    /// <summary>
    /// Class containing logic for parsing and packing modbus write coil functions/requests.
    /// </summary>
    public class WriteSingleCoilFunction : ModbusFunction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WriteSingleCoilFunction"/> class.
        /// </summary>
        /// <param name="commandParameters">The modbus command parameters.</param>
        public WriteSingleCoilFunction(ModbusCommandParameters commandParameters) : base(commandParameters)
        {
            CheckArguments(MethodBase.GetCurrentMethod(), typeof(ModbusWriteCommandParameters));
        }

        /// <inheritdoc />
        public override byte[] PackRequest()
        {
            byte[] request = new byte[12];

            ModbusWriteCommandParameters parameters = (ModbusWriteCommandParameters)CommandParameters;

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.TransactionId)), 0, request, 0, 2);

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.ProtocolId)), 0, request, 2, 2);

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.Length)), 0, request, 4, 2);

            request[6] = parameters.UnitId;
            request[7] = parameters.FunctionCode;

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parameters.OutputAddress)), 0, request, 8, 2);

            ushort valueToWrite = parameters.Value == 1 ? (ushort)0xFF00 : (ushort)0x0000;

            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)valueToWrite)), 0, request, 10, 2);

            return request;
        }

        /// <inheritdoc />
        public override Dictionary<Tuple<PointType, ushort>, ushort> ParseResponse(byte[] response)
        {
            Dictionary<Tuple<PointType, ushort>, ushort> retVal = new Dictionary<Tuple<PointType, ushort>, ushort>();

            ModbusWriteCommandParameters parameters = (ModbusWriteCommandParameters)CommandParameters;

            if (response[7] == parameters.FunctionCode + 0x80)
            {
                HandeException(response[8]);
            }

            ushort address = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(response, 8));

            ushort returnedValue = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(response, 10));

            ushort value = returnedValue == 0xFF00 ? (ushort)1 : (ushort)0;

            retVal.Add(new Tuple<PointType, ushort>(PointType.DIGITAL_OUTPUT, address), value);

            return retVal;
        }
    }
}