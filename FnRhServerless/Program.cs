using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
// CORREÇÃO: Alterado para o método esperado pelo .NET 8 Isolated
.ConfigureFunctionsWebApplication()
.ConfigureServices(services =>
{
    // Registra o HttpClient apontando para a URL da Web API no Azure (westus3)
    services.AddHttpClient("RhApi", client =>
    {
        client.BaseAddress = new Uri(
            Environment.GetEnvironmentVariable("RhApiBaseUrl")
            ?? "https://rhdiolab-f9byeseedwh7ckaq.westus3-01.azurewebsites.net"
        );
    });
})
.Build();

host.Run();
