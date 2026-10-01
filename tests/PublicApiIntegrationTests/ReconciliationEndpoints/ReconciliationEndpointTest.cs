using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.ReconciliationEndpoints;

[TestClass]
public class ReconciliationEndpointTest
{
    [TestMethod]
    public async Task ReturnsForbidden_ForNormalUser()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync($"api/reconciliation?from={from}&to={to}");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
