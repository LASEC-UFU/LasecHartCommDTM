using System;
using System.Xml;

namespace LasecHartCommDTM
{
    internal static class HartXmlHelper
    {
        // ================================================================
        // Parse do XML de requisição de transação HART (formato CWHart)
        // Formato: <DataExchangeRequest commandNumber="N" communicationReference="R">
        //            <fdt:CommunicationData byteArray="HEXDATA"/>
        //          </DataExchangeRequest>
        // ================================================================
        internal static void ParseTransactionRequest(string xml,
            out int commandNumber, out string communicationReference, out byte[] data)
        {
            commandNumber = 0;
            communicationReference = "0";
            data = new byte[0];

            if (string.IsNullOrEmpty(xml) || xml.Trim().Length == 0)
                return;

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                // Tenta encontrar DataExchangeRequest (formato CWHart)
                var node = FindNode(doc, "DataExchangeRequest");
                if (node != null)
                {
                    if (node.Attributes["commandNumber"] != null)
                        commandNumber = int.Parse(node.Attributes["commandNumber"].Value);
                    if (node.Attributes["communicationReference"] != null)
                        communicationReference = node.Attributes["communicationReference"].Value;

                    // Busca CommunicationData com byteArray
                    var dataNode = FindNode(doc, "CommunicationData");
                    if (dataNode != null && dataNode.Attributes["byteArray"] != null)
                    {
                        data = HexToBytes(dataNode.Attributes["byteArray"].Value.Trim());
                    }
                    return;
                }

                // Fallback: formato legado com atributo frame/data
                var legacyNode = doc.SelectSingleNode("//*[@frame]") ??
                                 doc.SelectSingleNode("//*[@data]");
                if (legacyNode != null)
                {
                    var hex = (legacyNode.Attributes["frame"] ?? legacyNode.Attributes["data"])?.Value ?? string.Empty;
                    data = HexToBytes(hex.Trim());
                }
            }
            catch (Exception ex)
            {
                CommDtm.Log("ParseTransactionRequest error: " + ex.Message);
            }
        }

        // ================================================================
        // Monta resposta de transação no formato CWHart: HartTransactionResp.xml
        // <DataExchangeResponse commandNumber="N" communicationReference="R">
        //   <fdt:CommunicationData byteArray="HEXDATA"/>
        //   <Status deviceStatus="0">
        //     <ResponseCode value="0"/>
        //   </Status>
        // </DataExchangeResponse>
        // ================================================================
        internal static string BuildTransactionResponse(int commandNumber,
            string communicationReference, byte[] responseData)
        {
            string hex = BytesToHex(responseData ?? new byte[0]);

            // Extrai status do dispositivo e código de resposta dos dados HART
            // (bytes de status ficam nas posições padrão do frame HART)
            string deviceStatus = "0";
            string responseCode = "0";

            if (responseData != null && responseData.Length >= 2)
            {
                // Os 2 primeiros bytes após o header normalmente contêm response code e device status
                responseCode = responseData[0].ToString();
                deviceStatus = responseData[1].ToString();
            }

            return
                "<?xml version=\"1.0\"?>" +
                "<FDT xmlns=\"x-schema:FDTHARTCommunicationSchema.xml\"" +
                " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                "<DataExchangeResponse" +
                " commandNumber=\"" + commandNumber + "\"" +
                " communicationReference=\"" + XmlEscape(communicationReference) + "\">" +
                  "<fdt:CommunicationData byteArray=\"" + hex + "\"/>" +
                  "<Status deviceStatus=\"" + deviceStatus + "\">" +
                    "<ResponseCode value=\"" + responseCode + "\"/>" +
                  "</Status>" +
                "</DataExchangeResponse>" +
                "</FDT>";
        }

        // ================================================================
        // Monta resposta de erro no formato CWHart: HartErrorResp.xml
        // <fdt:CommunicationError communicationError="TYPE" tag="TAG"/>
        // ================================================================
        internal static string BuildErrorResponse(string errorType, string tag)
        {
            return
                "<?xml version=\"1.0\"?>" +
                "<FDT xmlns=\"x-schema:FDTHARTCommunicationSchema.xml\"" +
                " xmlns:fdt=\"x-schema:FDTDataTypesSchema.xml\">" +
                "<fdt:CommunicationError" +
                " communicationError=\"" + XmlEscape(errorType) + "\"" +
                " tag=\"" + XmlEscape(tag) + "\"/>" +
                "</FDT>";
        }

        // ================================================================
        // Utilitários
        // ================================================================
        internal static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }

        internal static string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            if (hex.Length == 0) return new byte[0];
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static XmlNode FindNode(XmlDocument doc, string localName)
        {
            return FindNodeRecursive(doc.DocumentElement, localName);
        }

        private static XmlNode FindNodeRecursive(XmlNode node, string localName)
        {
            if (node == null) return null;
            if (node.LocalName == localName) return node;
            foreach (XmlNode child in node.ChildNodes)
            {
                var found = FindNodeRecursive(child, localName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
