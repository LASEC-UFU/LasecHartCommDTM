using System;
using System.Xml;

namespace LasecHartCommDTM
{
    internal static class HartXmlHelper
    {
        // Extrai bytes do frame HART do XML FDT de requisição
        internal static byte[] ParseRequestFrame(string xml)
        {
            if (string.IsNullOrEmpty(xml) || xml.Trim().Length == 0) return new byte[0];
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var node = doc.SelectSingleNode("//*[@frame]") ??
                           doc.SelectSingleNode("//*[@data]");
                if (node == null) return new byte[0];

                var hex = (node.Attributes["frame"] ?? node.Attributes["data"])?.Value ?? string.Empty;
                return HexToBytes(hex.Trim());
            }
            catch
            {
                return new byte[0];
            }
        }

        // Monta XML FDT de resposta com os bytes recebidos
        internal static string BuildResponseXml(byte[] response)
        {
            var hex = BytesToHex(response ?? new byte[0]);
            return
                "<FDTHARTCommunication xmlns=\"x-schema:FDTHARTCommunicationSchema.xml\">" +
                  "<fdt:TransactionResponse" +
                  "  frame=\"" + hex + "\"" +
                  "  xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\"/>" +
                "</FDTHARTCommunication>";
        }

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Replace(" ", "");
            if (hex.Length == 0) return new byte[0];
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static string BytesToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
