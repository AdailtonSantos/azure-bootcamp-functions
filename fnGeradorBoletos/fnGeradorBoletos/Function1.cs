using Azure.Messaging.ServiceBus;
using BarcodeStandard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace fnGeradorBoletos
{
    public class GeradorCodigoBarras
    {
        private readonly ILogger<GeradorCodigoBarras> _logger;
        private readonly string _serviceBusConnectionString;
        private readonly string _queueName = "gerador-codigo-barras";

        public GeradorCodigoBarras(ILogger<GeradorCodigoBarras> logger)
        {
            _logger = logger;
            _serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
        }

        [Function("barcode-generate")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                string valor = data?.valor;
                string dataVencimento = data?.dataVencimento;

                string barcodeData;

                if (string.IsNullOrEmpty(valor) || string.IsNullOrEmpty(dataVencimento))
                {
                    return new BadRequestObjectResult("Valor e data de vencimento são obrigatórios.");
                }

                // Validar formato da Data de Vencimento YYYY-MM-DD
                if (!DateTime.TryParseExact(dataVencimento, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime dateObj))
                {
                    return new BadRequestObjectResult("Data de vencimento inválida. Use o formato YYYY-MM-DD.");
                }
                string dateStr = dateObj.ToString("yyyyMMdd");

                // Conversão do Valor para centavos e formatação até 8 digitos.
                if (!decimal.TryParse(valor, out decimal valorDecimal) || valorDecimal < 0)
                {
                    return new BadRequestObjectResult("Valor inválido.");
                }
                int valorCentavos = (int)(valorDecimal * 10);
                string valorStr = valorCentavos.ToString("D8");

                string bankCode = "001";
                string baseCode = $"{bankCode}{dateStr}{valorStr}";

                // Preenchimento do barCode ter no máximo 44 caracteres
                barcodeData = baseCode.Length < 44 ? baseCode.PadRight(44, '0') : baseCode.Substring(0, 44);
                _logger.LogInformation($"Código de barras: {barcodeData}");

                Barcode barcode = new Barcode();
                var skImage = barcode.Encode(BarcodeStandard.Type.Code128, barcodeData);

                using (var encodeData = skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {   
                    var imageBytes = encodeData.ToArray();

                    string base64Image = Convert.ToBase64String(imageBytes);

                    var resultObject = new
                    {
                        barcode = barcodeData,
                        valorOriginal = valorDecimal,
                        dataVencimento = dataVencimento,
                        imageBase64 = base64Image
                    };
                    await SendFileFallback(resultObject, _serviceBusConnectionString, _queueName);
                    return new OkObjectResult(resultObject);
                }

            }
            catch (Exception ex)
            {
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task SendFileFallback(object resultObject, string serviceBusConnectionString, string queueName)
        {
            await using var client = new ServiceBusClient(serviceBusConnectionString);
            ServiceBusSender sender = client.CreateSender(queueName);
            string messageBody = JsonConvert.SerializeObject(resultObject);

            ServiceBusMessage message = new ServiceBusMessage(messageBody);
            await sender.SendMessageAsync(message);

            _logger.LogInformation($"Message sent to queue {queueName}: {messageBody}");
        }
    }
}
