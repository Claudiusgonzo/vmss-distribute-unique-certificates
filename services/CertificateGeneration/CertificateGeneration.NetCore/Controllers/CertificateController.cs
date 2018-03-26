﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using CertificateGeneration.Wrappers;

namespace CertificateGeneration.Controllers
{
    public class CertificateProperties
    {
        public string SubjectName { get; set; }
        public int ValidDays { get; set; }
        public string CertificateName { get; set; }
        public string SecretName { get; set; }
    }

    public class CertificatesRequest
    {
        public CertificateProperties[] CertificatesProperties { get; set; }
        public string IssuerBase64Pfx { get; set; }
        public string VaultBaseUrl { get; set; }
    }

    public class ResultJson
    {
        public string pfx { get; set; }
        public string result { get; set; }
    }

    [Route("api/[controller]")]
    public class CertificatesController : Controller
    {
        private IKVWrapper KvWrapper { get; set; }
        private ICertificatesWrapper CertificatesWrapper { get; set; }

        public CertificatesController(IKVWrapper kvWrapper, ICertificatesWrapper certificatesWrapper)
        {
            KvWrapper = kvWrapper;
            CertificatesWrapper = certificatesWrapper;
        }

        [HttpPost]
        public async Task<IActionResult> GenerateCertificatesAsync([FromBody] CertificatesRequest request)
        {
            // validate that we received a valid CertificatesRequest request body
            if (string.IsNullOrWhiteSpace(request?.VaultBaseUrl) 
                || request.CertificatesProperties == null 
                || request.CertificatesProperties.Length == 0)
            {
                return BadRequest();
            }

            try
            {
                X509Certificate2 issuerX509 = ! string.IsNullOrWhiteSpace(request.IssuerBase64Pfx)
                    ? new X509Certificate2(Convert.FromBase64String(request.IssuerBase64Pfx), "",
                        X509KeyStorageFlags.Exportable)
                    : null;

                var tasks = new List<Task<ResultJson>>();
                foreach (var properties in request.CertificatesProperties)
                {
                    tasks.Add(GenerateCertificateAsync(properties, request.VaultBaseUrl, issuerX509));
                }

                await Task.WhenAll(tasks);

                var result = new List<ResultJson>();
                foreach (var task in tasks)
                {
                    result.Add(task.Result);
                }

                return Ok(result);
            }
            catch (Exception exception)
            {
                //TODO: log exception
                Console.WriteLine(exception.Message);
                return StatusCode(500);
            }
        }

        private async Task<ResultJson> GenerateCertificateAsync(CertificateProperties properties, string vaultBaseUrl, X509Certificate2 issuerX509)
        {
            var result = new ResultJson();
            try
            {
                var x = CertificatesWrapper.GenerateCertificate(properties.SubjectName, properties.ValidDays, issuerX509);
                result.pfx = CertificatesWrapper.ExportToPfx(x);

                if (properties.CertificateName != "")
                {
                    await KvWrapper.UploadPfx(vaultBaseUrl, properties.CertificateName, result.pfx);
                }

                if (properties.SecretName != "")
                {
                    await KvWrapper.UploadPem(vaultBaseUrl, properties.SecretName, CertificatesWrapper.ExportToPEM(x));
                }

                result.result = "Success";
            }
            catch (Exception exception)
            {
                //TODO: log exception
                Console.WriteLine(exception);
                throw;
            }

            return result;
        }
    }
}
