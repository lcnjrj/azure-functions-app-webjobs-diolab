# 🏢 Sistema de RH Serverless — Azure Logic Apps + Functions + .NET 8

[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Azure Functions](https://img.shields.io/badge/Azure-Functions-blue)](https://azure.microsoft.com/)
[![Azure Logic Apps](https://img.shields.io/badge/Azure-Logic%20Apps-orange)](https://azure.microsoft.com/)
[![Linux](https://img.shields.io/badge/Linux-Lubuntu%2025.10-green)](https://lubuntu.me/)
[![DIO](https://img.shields.io/badge/DIO-Desafio%20de%20Projeto-orange)](https://www.dio.me/)

> Desafio da Trilha .NET — Nuvem com Microsoft Azure da DIO.
> Sistema de cadastro de funcionários com CRUD completo evoluído para arquitetura Serverless:
> entrada via HTTP → Logic App → Fila → Azure Function → API .NET → SQL + Logs.

![Desafio](/imagens/Print_DIO___Codifique_o_seu_futuro_global_agora___Google_Chrome_2026-06-17_151253.png)
---
## Arquitetura

```
Cliente HTTP (curl / Python)
        │
        │ POST + JSON
        ▼
┌─────────────────────┐
│   Azure Logic App   │  ← recebe o HTTP e devolve 202 imediatamente
│   (Http Trigger)    │
└────────┬────────────┘
         │ enfileira mensagem
         ▼
┌─────────────────────┐
│  Azure Queue Storage│  ← fila-rh (buffer contra picos de carga)
│  (fila-rh)          │
└────────┬────────────┘
         │ Queue Trigger (automático)
         ▼
┌─────────────────────┐
│   Azure Function    │  ← FnProcessarRH processa em segundo plano
│   (FnProcessarRH)   │
└────────┬────────────┘
         │ POST /Funcionario
         ▼
┌─────────────────────┐     ┌─────────────────────┐
│  Web API .NET 8     │────▶│  Azure SQL Database  │
│  (App Service)      │     │  (RhDioLab)          │
└─────────────────────┘     └─────────────────────┘
         │
         ▼
┌─────────────────────┐
│  Azure Table Storage│  ← FuncionarioLog (log de todas as operações)
└─────────────────────┘
```

---

## Tecnologias

| Tecnologia | Versão | Uso |
|-----------|--------|-----|
| .NET SDK | 8.0 | Web API e Azure Functions |
| ASP.NET Core Web API | 8.0 | CRUD de funcionários |
| Entity Framework Core | 6.x | ORM / Migrations |
| Azure Functions Core Tools | 4.x | Desenvolvimento e deploy de functions |
| Azure Logic Apps | Consumption | Orquestração visual do fluxo HTTP |
| Azure Queue Storage | — | Fila de mensagens assíncrona |
| Azure Table Storage | — | Log de operações (NoSQL) |
| Azure SQL Database | — | Banco de dados relacional |
| Azure App Service | F1 | Hospedagem da Web API |
| Azure CLI | 2.x | Deploy e gerenciamento via terminal |
| Python | 3.13 | Scripts de consulta de logs no terminal |
| Linux Lubuntu | 25.10 | Sistema operacional de desenvolvimento |

---

## Pré-requisitos no Lubuntu 25.10

Execute cada bloco no terminal (`Ctrl+Alt+T`) na ordem apresentada.

### 1. Atualizar o sistema

```bash
sudo apt update && sudo apt upgrade -y
```

### 2. Instalar o .NET 8 SDK

```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-8.0
dotnet --version
```

### 3. Instalar o Azure CLI

```bash
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az --version
```

### 4. Instalar Node.js e Azure Functions Core Tools

```bash
sudo apt install -y nodejs npm
npm install -g azure-functions-core-tools@4 --unsafe-perm true
func --version
```

### 5. Instalar ferramentas auxiliares

```bash
sudo apt install -y zip curl git
```

### 6. Instalar Entity Framework CLI

```bash
dotnet tool install --global dotnet-ef
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
source ~/.bashrc
dotnet ef --version
```

---

## Recursos criados no Azure

### Grupo de recursos

```
Nome:   desafiorh
Região: West US 2
```

### Lista de recursos

| Nome | Tipo | Finalidade |
|------|------|------------|
| `ASP-desafiorh-887e` | Plano do App Service (F1) | Hospeda a Web API |
| `rhdiolab` | App Service | Web API .NET em execução |
| `desafio-rh-azure` | SQL Server | Servidor do banco |
| `RhDioLab` | SQL Database | Tabela de funcionários |
| `logrhdiolab` | Storage Account | Fila + Table Storage |
| `fn-rh-serverless` | Function App | Azure Function |
| `logic-rh-entrada` | Logic App | Gatilho HTTP e enfileiramento |

![Recusros](/imagens/azure-functions-app-webjobs-diolab/blob/main/imagens/Print_DIO___Codifique_o_seu_futuro_global_agora___Google_Chrome_2026-06-17_151253.png)


---

## Parte 1 — Web API .NET

### Estrutura do projeto

```bash
mkdir desafio-rh-serverless && cd desafio-rh-serverless
dotnet new webapi -n RhApi
cd RhApi
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Azure.Data.Tables
```

### Models/Funcionario.cs

```csharp
namespace RhApi.Models;

public class Funcionario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Ramal { get; set; } = string.Empty;
    public string EmailProfissional { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public decimal Salario { get; set; }
    public DateTimeOffset DataAdmissao { get; set; }
}
```

### Models/FuncionarioLog.cs

```csharp
using Azure;
using Azure.Data.Tables;

namespace RhApi.Models;

public enum TipoAcao { Inclusao = 0, Atualizacao = 1, Remocao = 2 }

public class FuncionarioLog : ITableEntity
{
    public string PartitionKey { get; set; } = "FuncionarioLog";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public int FuncionarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public TipoAcao TipoAcao { get; set; }
    public string JSON { get; set; } = string.Empty;
}
```

### Data/RhContext.cs

```csharp
using Microsoft.EntityFrameworkCore;
using RhApi.Models;

namespace RhApi.Data;

public class RhContext : DbContext
{
    public RhContext(DbContextOptions<RhContext> options) : base(options) { }
    public DbSet<Funcionario> Funcionarios { get; set; }
}
```

### Controllers/FuncionarioController.cs

```csharp
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RhApi.Data;
using RhApi.Models;
using System.Text.Json;

namespace RhApi.Controllers;

[ApiController]
[Route("[controller]")]
public class FuncionarioController : ControllerBase
{
    private readonly RhContext _context;
    private readonly TableClient _tableClient;

    public FuncionarioController(RhContext context, IConfiguration config)
    {
        _context = context;
        var connStr = config.GetConnectionString("SAConnectionString")!;
        var tableName = config.GetConnectionString("AzureTableName") ?? "FuncionarioLog";
        _tableClient = new TableClient(connStr, tableName);
        _tableClient.CreateIfNotExists();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var func = await _context.Funcionarios.FindAsync(id);
        if (func is null) return NotFound();
        return Ok(func);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Funcionario funcionario)
    {
        _context.Funcionarios.Add(funcionario);
        await _context.SaveChangesAsync();
        await SalvarLog(funcionario, TipoAcao.Inclusao);
        return CreatedAtAction(nameof(Get), new { id = funcionario.Id }, funcionario);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] Funcionario funcionario)
    {
        var existente = await _context.Funcionarios.FindAsync(id);
        if (existente is null) return NotFound();
        existente.Nome = funcionario.Nome;
        existente.Endereco = funcionario.Endereco;
        existente.Ramal = funcionario.Ramal;
        existente.EmailProfissional = funcionario.EmailProfissional;
        existente.Departamento = funcionario.Departamento;
        existente.Salario = funcionario.Salario;
        existente.DataAdmissao = funcionario.DataAdmissao;
        await _context.SaveChangesAsync();
        await SalvarLog(existente, TipoAcao.Atualizacao);
        return Ok(existente);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var func = await _context.Funcionarios.FindAsync(id);
        if (func is null) return NotFound();
        _context.Funcionarios.Remove(func);
        await _context.SaveChangesAsync();
        await SalvarLog(func, TipoAcao.Remocao);
        return NoContent();
    }

    private async Task SalvarLog(Funcionario func, TipoAcao acao)
    {
        var log = new FuncionarioLog
        {
            FuncionarioId = func.Id,
            Nome = func.Nome,
            Departamento = func.Departamento,
            TipoAcao = acao,
            JSON = JsonSerializer.Serialize(func)
        };
        await _tableClient.AddEntityAsync(log);
    }
}
```

### Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using RhApi.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<RhContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConexaoPadrao")));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "ConexaoPadrao": "Server=tcp:desafio-rh-azure.database.windows.net,1433;Initial Catalog=RhDioLab;User ID=SEU_USUARIO;Password=SUA_SENHA;Encrypt=True;",
    "SAConnectionString": "DefaultEndpointsProtocol=https;AccountName=logrhdiolab;AccountKey=SUA_CHAVE;EndpointSuffix=core.windows.net",
    "AzureTableName": "FuncionarioLog"
  }
}
```

> ⚠️ Nunca suba credenciais reais para o GitHub. Use variáveis de ambiente do App Service em produção.
Tudo isso já foi excluido.

### Migration e execução local

```bash
dotnet ef migrations add CriacaoInicial
dotnet ef database update \
  --connection "Server=tcp:desafio-rh-azure.database.windows.net,1433;Initial Catalog=RhDioLab;User ID=SEU_USUARIO;Password=SUA_SENHA;Encrypt=True;"
dotnet run
# Acesse: http://localhost:5000/swagger
```
 print do Swagger mostrando POST

---

## Parte 2 — Azure Logic App

O Logic App é configurado pelo portal Azure — não pelo CLI.

### Criar o Logic App

```
Portal Azure → Criar um recurso → Logic App
  Nome:   logic-rh-entrada
  Região: West US 2
  Tipo:   Consumo (Consumption)
→ Criar
```

print  logic-rh-entrada

### Configurar o gatilho HTTP

```
logic-rh-entrada
  → Designer do aplicativo lógico
    → Adicionar um gatilho
      → Pesquisar: "When a HTTP request is received"
        → Selecionar
```

Cole o schema de exemplo e clique em "Gerar esquema":

```json
{
  "nome": "João Silva",
  "endereco": "Rua 1234",
  "ramal": "1234",
  "emailProfissional": "joao@email.com",
  "departamento": "TI",
  "salario": 5000,
  "dataAdmissao": "2024-01-15T00:00:00.000Z"
}
```

Salve — a URL do endpoint é gerada automaticamente. Anote essa URL.

### Adicionar a ação de fila

```
+ Nova etapa
  → Pesquisar: "Filas do Azure"    ← nome em português no portal
    → Ação: "Colocar uma mensagem em uma fila (V2)"
      → Criar conexão:
          Nome:             conexao-fila-rh
          Conta:            logrhdiolab
          Chave de acesso:  (copiar de logrhdiolab → Chaves de acesso → key1)
      → Nome da fila:  fila-rh
      → Mensagem:      Body  (conteúdo dinâmico do gatilho HTTP)
→ Salvar
```

> ⚠️ **Atenção ao nome do conector:** o portal em português chama de **"Filas do Azure"**,
> não "Azure Queue Storage". A ação correta é a versão **(V2)** — a sem V2 está descontinuada.

 print do designer mostrando o gatilho HTTP e a ação de fila 

 print do campo "URL do HTTP POST" gerado pelo Logic App

---

## Parte 3 — Azure Function

### Criar o projeto

```bash
cd ..   # volta para desafio-rh-serverless
func init FnRhServerless --worker-runtime dotnet-isolated --target-framework net8.0
cd FnRhServerless
dotnet add package Microsoft.Azure.Functions.Worker
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Http
dotnet add package Microsoft.Extensions.Http
```

### FnProcessarRH.cs

```csharp
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FnRhServerless;

public record FuncionarioDto(
    string Nome, string Endereco, string Ramal,
    string EmailProfissional, string Departamento,
    decimal Salario, DateTimeOffset DataAdmissao);

public class FnProcessarRH
{
    private readonly ILogger<FnProcessarRH> _logger;
    private readonly HttpClient _httpClient;

    public FnProcessarRH(ILogger<FnProcessarRH> logger, IHttpClientFactory factory)
    {
        _logger = logger;
        _httpClient = factory.CreateClient("RhApi");
    }

    [Function("FnProcessarRH")]
    public async Task Run(
        [QueueTrigger("fila-rh", Connection = "AzureWebJobsStorage")] string mensagem)
    {
        _logger.LogInformation("Mensagem recebida: {msg}", mensagem);
        var funcionario = JsonSerializer.Deserialize<FuncionarioDto>(mensagem,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (funcionario is null) { _logger.LogError("JSON inválido."); return; }

        var resposta = await _httpClient.PostAsJsonAsync("/Funcionario", funcionario);

        if (resposta.IsSuccessStatusCode)
            _logger.LogInformation("Salvo: {r}", await resposta.Content.ReadAsStringAsync());
        else
            _logger.LogError("Erro: {s}", resposta.StatusCode);
    }
}
```

### Program.cs da Function

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient("RhApi", client =>
        {
            client.BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("RhApiBaseUrl")
                    ?? "https://rhdiolab-f9byeseedwh7ckaq.westus2-01.azurewebsites.net");
        });
    })
    .Build();

host.Run();
```

### local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=logrhdiolab;AccountKey=SUA_CHAVE;EndpointSuffix=core.windows.net",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "RhApiBaseUrl": "https://rhdiolab-f9byeseedwh7ckaq.westus2-01.azurewebsites.net"
  }
}
```

### Testar localmente

```bash
func start
```

---

## Parte 4 — Deploy

### Criar recursos no Azure via terminal

```bash
# Login
az login

# Grupo de recursos (se não existir)
az group create --name desafiorh --location westus2

# Storage Account (se não existir)
az storage account create \
  --name logrhdiolab \
  --resource-group desafiorh \
  --location westus2 \
  --sku Standard_LRS

# Criar a fila
CHAVE=$(az storage account keys list \
  --account-name logrhdiolab \
  --resource-group desafiorh \
  --query "[0].value" \
  --output tsv)

az storage queue create \
  --name fila-rh \
  --account-name logrhdiolab \
  --account-key "$CHAVE"

# Criar o Function App
az functionapp create \
  --name fn-rh-serverless \
  --resource-group desafiorh \
  --storage-account logrhdiolab \
  --consumption-plan-location westus2 \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4
```

### Deploy da Web API

```bash
cd desafio-rh-serverless/RhApi
dotnet publish -c Release -o ./publish
cd publish && zip -r ../app.zip . && cd ..

az webapp deployment source config-zip \
  --resource-group desafiorh \
  --name rhdiolab \
  --src app.zip
```

Configurar connection strings no App Service:

```
Portal Azure → rhdiolab → Configurações → Variáveis de ambiente → Cadeias de conexão
```

| Nome | Tipo |
|------|------|
| `ConexaoPadrao` | SQLAzure |
| `SAConnectionString` | Custom |
| `AzureTableName` | Custom |

### 📸 Print 6 — Connection strings configuradas no App Service

> Insira aqui o print das variáveis de ambiente no App Service

### Deploy da Azure Function

```bash
cd desafio-rh-serverless/FnRhServerless
func azure functionapp publish fn-rh-serverless
```

Configurar variáveis no Function App:

```
Portal Azure → fn-rh-serverless → Configurações → Configurações do aplicativo
```

| Nome | Valor |
|------|-------|
| `AzureWebJobsStorage` | (connection string do logrhdiolab) |
| `RhApiBaseUrl` | https://rhdiolab-f9byeseedwh7ckaq.westus2-01.azurewebsites.net |

 print do fn-rh-serverless mostrando a function FnProcessarRH ativa

---

## Testando o fluxo

### Enviar funcionário via curl

```bash
curl -X POST \
  "URL_DO_LOGIC_APP_AQUI" \
  -H "Content-Type: application/json" \
  -d '{
    "nome": "Maria Souza",
    "endereco": "Av. Paulista, 1000",
    "ramal": "4321",
    "emailProfissional": "maria@empresa.com",
    "departamento": "RH",
    "salario": 6500,
    "dataAdmissao": "2024-03-01T00:00:00.000Z"
  }'
```

Resposta esperada: `HTTP 202 Accepted`

print do terminal com o curl e a resposta 202 Accepted

### Verificar no histórico do Logic App

```
Portal Azure → logic-rh-entrada → Histórico de Execuções
```

print do histórico mostrando execuções com ✅ Êxito

print do detalhe de execução mostrando o gatilho HTTP e a ação de fila em verde

### Confirmar funcionário salvo via GET

```bash
curl https://rhdiolab-f9byeseedwh7ckaq.westus2-01.azurewebsites.net/Funcionario/1
```

print do terminal com a resposta JSON do GET

---

## Consultando os logs

### Ver registros da tabela FuncionarioLog

```bash
CHAVE=$(az storage account keys list \
  --account-name logrhdiolab \
  --resource-group desafiorh \
  --query "[0].value" \
  --output tsv)

az storage entity query \
  --table-name FuncionarioLog \
  --account-name logrhdiolab \
  --account-key "$CHAVE" \
  --output json | python3 -c "
import json, sys
itens = json.load(sys.stdin)['items']
print(f\"{'Departamento':<15} {'Nome':<25} {'Acao':<12} {'Timestamp':<22}\")
print('-' * 76)
for i in itens:
    print(f\"{i.get('PartitionKey',''):<15} {i.get('Nome',''):<25} {i.get('TipoAcao',''):<12} {i.get('Timestamp','')[:19]:<22}\")
print(f'\nTotal: {len(itens)} registros')
"
```

> Onde TipoAcao: 0 = Inclusao | 1 = Atualizacao | 2 = Remocao

 print do terminal com a tabela de logs exibida

### Ver logs no portal

```
Portal Azure → logrhdiolab → Navegador de armazenamento → Tabelas → FuncionarioLog
```
 print da tabela FuncionarioLog com os 18+ registros visíveis

### Ver fila (deve estar vazia — sinal de sucesso)

```
Portal Azure → logrhdiolab → Navegador de armazenamento → Filas → fila-rh
```
 print da fila vazia — fila vazia = Function processou tudo com sucesso

---

![Excluindo tudo:](/imagens/Print_Excluir_recursos_MS_Azure_Chrome_2026-06-17_220502.png)
__

## Dificuldades e soluções

### 1. Designer do Logic App com interface diferente da documentação

**Problema:** o portal 2025 usa painel lateral. A caixa de busca fica escondida acima da lista de conectores.

**Solução:** rolar o painel para cima antes de pesquisar. O conector se chama **"Filas do Azure"** em português. Usar sempre a ação **(V2)**.

---

### 2. Chave de acesso do Storage exposta

**Problema:** chave copiada manualmente apareceu em logs e prints.

**Solução:** regenerar a chave imediatamente no portal. Nunca mais hardcodar — usar sempre:

```bash
CHAVE=$(az storage account keys list \
  --account-name logrhdiolab \
  --resource-group desafiorh \
  --query "[0].value" \
  --output tsv)
```

---

### 3. `--query` do CLI não filtrava entidades da tabela

**Problema:** parâmetro `--query` com JMESPath não funcionava para o comando `az storage entity query`.

**Solução:** usar `--output json` com pipe para Python inline.

---

### 4. `json.load(sys)` quebrou no Python 3.13

**Problema:** `AttributeError: module 'sys' has no attribute 'read'`

**Solução:** trocar `json.load(sys)` por `json.load(sys.stdin)`.

---

### 5. Fila vazia pareceu erro mas era sucesso

**Problema:** fila `fila-rh` sempre aparecia vazia no portal — parecia que o Logic App não estava funcionando.

**Solução:** fila vazia = mensagens consumidas pela Function em milissegundos. A prova real do funcionamento é a tabela `FuncionarioLog` com os registros salvos.

---

## Estrutura de pastas

```
desafio-rh-serverless/
├── RhApi/
│   ├── Controllers/FuncionarioController.cs
│   ├── Data/RhContext.cs
│   ├── Models/Funcionario.cs
│   ├── Models/FuncionarioLog.cs
│   ├── Migrations/
│   ├── appsettings.json        ⚠️ não suba com credenciais reais
│   ├── Program.cs
│   └── RhApi.csproj
│
├── FnRhServerless/
│   ├── FnProcessarRH.cs
│   ├── local.settings.json     ⚠️ não suba no Git
│   ├── Program.cs
│   └── FnRhServerless.csproj
│
└── README.md
```

---

## Endpoints da API

**URL Base:** `https://rhdiolab-f9byeseedwh7ckaq.westus2-01.azurewebsites.net`

| Verbo | Endpoint | Descrição |
|-------|----------|-----------|
| `GET` | `/Funcionario/{id}` | Buscar por ID |
| `POST` | `/Funcionario` | Criar funcionário |
| `PUT` | `/Funcionario/{id}` | Atualizar funcionário |
| `DELETE` | `/Funcionario/{id}` | Remover funcionário |

---
