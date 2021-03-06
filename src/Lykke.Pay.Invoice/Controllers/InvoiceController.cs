﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Lykke.AzureRepositories;
using Lykke.AzureRepositories.Azure.Tables;
using Lykke.Core;
using Lykke.Pay.Invoice.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Lykke.Pay.Invoice.Controllers
{
    public class InvoiceController : BaseController
    {
       

        public InvoiceController(IConfiguration configuration) : base(configuration)
        {

        }


        public IActionResult Index()
        {
            return NotFound();
        }
        [Route("{InvoiceId}")]
        public async Task<IActionResult> Index(string invoiceId)
        {
            var model = new InvoiceResult();
            var inv = await InvoiceRequestRepo.GetInvoice(invoiceId);
            if (inv == null)
            {
                return NotFound();
            }


            model.OrigAmount = inv.Amount;
            model.Currency = inv.Currency;
            model.InvoiceNumber = inv.InvoiceNumber;

            var order = new
            {
                inv.Currency,
                inv.Amount,
                ExchangeCurrency = "BTC",
                OrderId = inv.InvoiceNumber,
                Markup = new
                {
                    Percent = 1,
                    Pips = 10
                }
            };




            var bodyRequest = JsonConvert.SerializeObject(order);
            var strToSign = string.Format("{0}{1}", MerchantApiKey, bodyRequest);


            var csp = CreateRsaFromPrivateKey(MerchantPrivateKey);//certificate.GetRSAPrivateKey();
            var sign = Convert.ToBase64String(csp.SignData(Encoding.UTF8.GetBytes(strToSign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Lykke-Merchant-Id", MerchantId);
            httpClient.DefaultRequestHeaders.Add("Lykke-Merchant-Sign", sign);
            

            var result = await httpClient.PostAsync(LykkePayOrderUrl,
                new StringContent(bodyRequest, Encoding.UTF8, "application/json"));
            var resp = await result.Content.ReadAsStringAsync();



            if (result.StatusCode != HttpStatusCode.OK)
            {
                return BadRequest();
            }

            dynamic orderResp = JsonConvert.DeserializeObject(resp);

            model.Amount = orderResp.amount;
            model.QRCode =
                $@"https://chart.googleapis.com/chart?chs=220x220&chld=L|2&cht=qr&chl=bitcoin:{orderResp.address}?amount={orderResp.amount}%26label=LykkePay%26message={orderResp.orderId}";
            return View(model);

        }

        private static RSA CreateRsaFromPrivateKey(string privateKey)
        {
            privateKey = privateKey.Replace("-----BEGIN RSA PRIVATE KEY-----", "").Replace("-----END RSA PRIVATE KEY-----", "").Replace("\n", "");
            var privateKeyBits = System.Convert.FromBase64String(privateKey);


            var RSAparams = new RSAParameters();

            using (BinaryReader binr = new BinaryReader(new MemoryStream(privateKeyBits)))
            {
                byte bt = 0;
                ushort twobytes = 0;
                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130)
                    binr.ReadByte();
                else if (twobytes == 0x8230)
                    binr.ReadInt16();
                else
                    throw new Exception("Unexpected value read binr.ReadUInt16()");

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102)
                    throw new Exception("Unexpected version");

                bt = binr.ReadByte();
                if (bt != 0x00)
                    throw new Exception("Unexpected value read binr.ReadByte()");

                RSAparams.Modulus = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.Exponent = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.D = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.P = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.Q = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.DP = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.DQ = binr.ReadBytes(GetIntegerSize(binr));
                RSAparams.InverseQ = binr.ReadBytes(GetIntegerSize(binr));
            }


            return RSA.Create(RSAparams);
        }

        private static int GetIntegerSize(BinaryReader binr)
        {
            byte bt = 0;
            byte lowbyte = 0x00;
            byte highbyte = 0x00;
            int count = 0;
            bt = binr.ReadByte();
            if (bt != 0x02)
                return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();
            else
            if (bt == 0x82)
            {
                highbyte = binr.ReadByte();
                lowbyte = binr.ReadByte();
                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                count = BitConverter.ToInt32(modint, 0);
            }
            else
            {
                count = bt;
            }

            while (binr.ReadByte() == 0x00)
            {
                count -= 1;
            }
            binr.BaseStream.Seek(-1, SeekOrigin.Current);
            return count;
        }
    }
}
