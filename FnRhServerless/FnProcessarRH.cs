using System.Text.Json;
using System.Net.Http.Json; // <-- CORREÇÃO: Necessário para o PostAsJsonAsync funcionar
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FnRhServerless;

// Modelo do funcionário (igual ao da API)
public record FuncionarioDto(
    string Nome,
    string Endereco,
    string Ramal,
    string EmailProfissional,
    string Departamento,
    decimal Salario,
    DateTimeOffset DataAdmissao
);

public class FnProcessarRH
{
    private readonly ILogger<FnProcessarRH> _logger;
    private readonly HttpClient _httpClient;

    public FnProcessarRH(ILogger<FnProcessarRH> logger, IHttpClientFactory factory)
    {
        _logger = logger;
        _httpClient = factory.CreateClient("RhApi");
    }

    // Esta função é ativada automaticamente quando chega mensagem na fila
    [Function("FnProcessarRH")]
    public async Task Run(
        [QueueTrigger("fila-rh", Connection = "AzureWebJobsStorage")] string mensagem)
    {
        _logger.LogInformation("Mensagem recebida da fila: {msg}", mensagem);

        try
        {
            // Desserializar o JSON que veio do Logic App
            var funcionario = JsonSerializer.Deserialize<FuncionarioDto>(mensagem,
                                                                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (funcionario is null)
            {
                _logger.LogError("Mensagem inválida — não foi possível desserializar.");
                return;
            }

            // Chamar a Web API .NET para salvar no SQL
            var resposta = await _httpClient.PostAsJsonAsync("/Funcionario", funcionario);

            if (resposta.IsSuccessStatusCode)
            {
                var conteudo = await resposta.Content.ReadAsStringAsync();
                _logger.LogInformation("Funcionário salvo com sucesso: {resp}", conteudo);
            }
            else
            {
                _logger.LogError("Erro ao salvar funcionário. Status: {status}", resposta.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar mensagem da fila.");
            throw; // Relança para o Azure reprocessar automaticamente
        }
    }
}
