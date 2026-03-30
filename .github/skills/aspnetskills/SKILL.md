---
name: aspnetskills
description: Boas práticas para criação e manutenção de projetos ASP.NET com C#. Abrange SOLID, Clean Code, arquitetura, refatoração, padrões de projeto, segurança, testes e performance. Use quando estiver criando, revisando ou refatorando código C#/ASP.NET.
---

# ASP.NET & C# — Guia de Boas Práticas

Este skill define as diretrizes obrigatórias para todo código C# e ASP.NET gerado ou modificado neste workspace.

---

## 1. Princípios SOLID

Sempre aplique os cinco princípios SOLID:

- **S — Single Responsibility**: cada classe e método deve ter uma única responsabilidade. Se um método faz mais de uma coisa, divida-o.
- **O — Open/Closed**: classes devem ser abertas para extensão e fechadas para modificação. Prefira abstrações e composição.
- **L — Liskov Substitution**: subtipos devem ser substituíveis por seus tipos base sem alterar o comportamento esperado.
- **I — Interface Segregation**: crie interfaces pequenas e específicas. Nunca force um consumidor a depender de métodos que não utiliza.
- **D — Dependency Inversion**: dependa de abstrações (interfaces), não de implementações concretas. Use injeção de dependência (DI) do ASP.NET.

```csharp
// BOM — depende de abstração
public class MonitoramentoService : IMonitoramentoService
{
    private readonly IPsnIntegrationService _psnService;
    public MonitoramentoService(IPsnIntegrationService psnService) => _psnService = psnService;
}

// RUIM — depende de implementação concreta
public class MonitoramentoService
{
    private readonly PsnIntegrationService _psnService = new();
}
```

---

## 2. Clean Code — Código Compreensível e Elegante

### 2.1 Nomenclatura

- Use nomes descritivos e pronunciáveis: `ObterPrecoPorJogoId` em vez de `GetP`.
- Classes: `PascalCase` (ex: `AlertaRequestDTO`).
- Métodos e propriedades: `PascalCase`.
- Variáveis locais e parâmetros: `camelCase`.
- Constantes: `PascalCase` (ex: `MaxTentativas`).
- Interfaces: prefixo `I` (ex: `IPsnIntegrationService`).
- Evite abreviações ambíguas. Prefira clareza à brevidade.

### 2.2 Métodos

- Mantenha métodos curtos — idealmente até **20 linhas**.
- Um método deve fazer **uma única coisa**.
- Limite a no máximo **3 parâmetros**. Se precisar de mais, encapsule em um objeto (DTO/record).
- Prefira `early return` para reduzir aninhamento:

```csharp
// BOM
public async Task<IActionResult> ObterAlerta(int id)
{
    var alerta = await _service.BuscarPorIdAsync(id);
    if (alerta is null)
        return NotFound();

    return Ok(alerta);
}

// RUIM — aninhamento desnecessário
public async Task<IActionResult> ObterAlerta(int id)
{
    var alerta = await _service.BuscarPorIdAsync(id);
    if (alerta is not null)
    {
        return Ok(alerta);
    }
    else
    {
        return NotFound();
    }
}
```

### 2.3 Legibilidade

- Use linhas em branco para separar blocos lógicos.
- Não comente código óbvio. O código deve ser autoexplicativo.
- Se um trecho precisa de comentário, considere renomear variáveis/métodos para torná-lo claro.
- Comente apenas o **porquê**, nunca o **o quê**.

---

## 3. Arquitetura e Organização de Projeto

### 3.1 Estrutura de Pastas

Organize o projeto por responsabilidade:

```
Controllers/       → Endpoints da API (finos, sem lógica de negócio)
Services/          → Lógica de negócio
Interfaces/        → Contratos (interfaces)
Models/            → DTOs, entidades, value objects
Integrations/      → Comunicação com serviços externos
Middlewares/       → Middlewares customizados
Repositories/      → Acesso a dados (quando aplicável)
```

### 3.2 Controllers

- Controllers devem ser **finos**: apenas recebem a requisição, delegam ao service e retornam o resultado.
- Nunca coloque lógica de negócio no controller.
- Use `[ApiController]` e tipagem forte nos retornos (`ActionResult<T>`).

```csharp
[ApiController]
[Route("api/[controller]")]
public class AlertasController : ControllerBase
{
    private readonly IMonitoramentoService _service;

    public AlertasController(IMonitoramentoService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<AlertaResponseDTO>> Criar([FromBody] AlertaRequestDTO request)
    {
        var resultado = await _service.CriarAlertaAsync(request);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }
}
```

### 3.3 Injeção de Dependência

- Registre todos os serviços no DI container (`Program.cs`).
- Use o ciclo de vida adequado: `Scoped` para services com estado por requisição, `Singleton` para stateless, `Transient` para leves e descartáveis.

---

## 4. Refatoração Retroativa Contínua

**Sempre** que tocar em um arquivo, avalie se há oportunidade de refatoração:

- **Extraia métodos** quando um bloco faz mais de uma coisa.
- **Elimine código duplicado** — aplique DRY (Don't Repeat Yourself).
- **Simplifique condicionais** complexas com guard clauses ou pattern matching.
- **Remova código morto**: métodos não utilizados, usings desnecessários, variáveis não referenciadas.
- **Reduza acoplamento**: se uma classe conhece detalhes internos de outra, introduza uma interface.
- **Prefira composição a herança**.

```csharp
// ANTES — condicional complexa
if (preco != null && preco.ValorAtual > 0 && preco.ValorAtual <= alerta.PrecoDesejado && alerta.Ativo)
{
    await _telegram.EnviarNotificacaoAsync(alerta);
}

// DEPOIS — extraído para método com nome claro
if (DeveNotificar(preco, alerta))
    await _telegram.EnviarNotificacaoAsync(alerta);

private static bool DeveNotificar(PrecoPsnDTO? preco, Alerta alerta)
    => preco is { ValorAtual: > 0 }
       && preco.ValorAtual <= alerta.PrecoDesejado
       && alerta.Ativo;
```

---

## 5. Tratamento de Erros

- Use exceções para situações **excepcionais**, não para fluxo de controle.
- Crie exceções de domínio específicas quando necessário (`JogoNaoEncontradoException`).
- Use middleware global de tratamento de erros para padronizar respostas de erro da API.
- Sempre faça log de exceções com contexto suficiente.
- Nunca engula exceções silenciosamente (`catch { }`).

```csharp
// Middleware global de erro
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var error = context.Features.Get<IExceptionHandlerFeature>();
        if (error is not null)
        {
            logger.LogError(error.Error, "Erro não tratado");
            await context.Response.WriteAsJsonAsync(new { mensagem = "Erro interno do servidor." });
        }
    });
});
```

---

## 6. Async/Await

- Use `async/await` para todas as operações de I/O (HTTP, banco, arquivos).
- Nunca use `.Result` ou `.Wait()` — causa deadlocks.
- Sufixe métodos assíncronos com `Async` (ex: `BuscarPrecosAsync`).
- Use `CancellationToken` em operações que podem ser canceladas.

```csharp
public async Task<PrecoPsnDTO?> BuscarPrecoAsync(string jogoId, CancellationToken cancellationToken = default)
{
    var response = await _httpClient.GetAsync($"/api/jogos/{jogoId}", cancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<PrecoPsnDTO>(cancellationToken: cancellationToken);
}
```

---

## 7. Validação

- Valide entrada na borda do sistema (controllers/DTOs).
- Use Data Annotations ou FluentValidation para validação declarativa.
- Retorne `400 Bad Request` com mensagens claras para entrada inválida.

```csharp
public record AlertaRequestDTO
{
    [Required(ErrorMessage = "O ID do jogo é obrigatório.")]
    public string JogoId { get; init; } = string.Empty;

    [Range(0.01, 10000, ErrorMessage = "O preço desejado deve estar entre 0.01 e 10000.")]
    public decimal PrecoDesejado { get; init; }
}
```

---

## 8. Segurança

- Nunca exponha segredos no código. Use `appsettings.json`, variáveis de ambiente ou Azure Key Vault.
- Valide e sanitize toda entrada do usuário.
- Use HTTPS sempre.
- Implemente autenticação/autorização adequada (API Key, JWT, OAuth).
- Proteja contra os riscos do OWASP Top 10 (injection, XSS, CSRF, etc.).
- Use `[Authorize]` nos endpoints que exigem autenticação.

---

## 9. Logging e Observabilidade

- Use `ILogger<T>` injetado via DI — nunca `Console.WriteLine`.
- Defina o nível de log adequado: `Information` para fluxo normal, `Warning` para situações inesperadas mas tratáveis, `Error` para falhas.
- Inclua contexto estruturado nos logs:

```csharp
_logger.LogInformation("Verificando preço do jogo {JogoId} para alerta {AlertaId}", jogoId, alertaId);
```

---

## 10. Padrões e Convenções C# Modernas

- Use `record` para DTOs imutáveis.
- Use `required` e `init` para propriedades obrigatórias.
- Use pattern matching (`is`, `switch expressions`) para simplificar condicionais.
- Use `string interpolation` em vez de concatenação.
- Use `collection expressions` (`[1, 2, 3]`) quando disponível (.NET 8+).
- Use `global usings` para namespaces frequentes.
- Use `file-scoped namespaces` para reduzir indentação.

```csharp
// file-scoped namespace
namespace PsnPriceTracker.Services;

public class MonitoramentoService : IMonitoramentoService
{
    // ...
}
```

---

## 11. Testes

- Escreva testes unitários para lógica de negócio nos services.
- Use xUnit como framework de testes.
- Nomeie testes descritivamente: `DeveNotificar_QuandoPrecoAbaixoDoDesejado_EAlertaAtivo`.
- Use Moq ou NSubstitute para mocks.
- Siga o padrão AAA (Arrange, Act, Assert).

```csharp
[Fact]
public async Task DeveEnviarNotificacao_QuandoPrecoAtingeValorDesejado()
{
    // Arrange
    var telegramMock = new Mock<ITelegramIntegrationService>();
    var service = new MonitoramentoService(telegramMock.Object);

    // Act
    await service.VerificarPrecosAsync();

    // Assert
    telegramMock.Verify(t => t.EnviarNotificacaoAsync(It.IsAny<string>()), Times.Once);
}
```

---

## 12. Performance

- Use `IHttpClientFactory` em vez de instanciar `HttpClient` diretamente.
- Prefira `AsNoTracking()` para queries de leitura no EF Core.
- Use cache (`IMemoryCache` ou distributed cache) para dados que mudam pouco.
- Evite alocações desnecessárias — use `Span<T>`, `ReadOnlySpan<T>` quando aplicável.
- Nunca faça chamadas síncronas bloqueantes em contextos assíncronos.

---

## Resumo — Checklist Rápido

Antes de finalizar qualquer alteração, verifique:

- [ ] O código segue os princípios SOLID?
- [ ] Métodos são curtos e fazem uma única coisa?
- [ ] Nomes são claros e descritivos?
- [ ] Há código duplicado que pode ser extraído?
- [ ] Código morto foi removido?
- [ ] Controllers estão finos (sem lógica de negócio)?
- [ ] Dependências são injetadas via DI (interfaces)?
- [ ] Operações de I/O usam async/await?
- [ ] Entrada é validada na borda do sistema?
- [ ] Segredos estão fora do código-fonte?
- [ ] Logs usam `ILogger` com contexto estruturado?
- [ ] Refatorações retroativas foram aplicadas nos arquivos tocados?
