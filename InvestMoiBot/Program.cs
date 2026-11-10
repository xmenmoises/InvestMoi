
using InvestMoiBot.Services;
using System.Text.Json;

using var http = new HttpClient();

var scraper = new FundamentusScraper(http);

string caminhoLocal = @"C:\Users\positivo.moises\Documents\GitHub\InvestMoi\wwwroot";
string pasta = Path.Combine(caminhoLocal, "data", "acoes");
string caminhoIndex = Path.Combine(pasta, "index.json");

Directory.CreateDirectory(pasta);

var opcoesJson = new JsonSerializerOptions
{
    WriteIndented = true
};

// MENU PRINCIPAL
while (true)
{
    Console.WriteLine("\n==================================");
    Console.WriteLine("          INVESTMOI BOT");
    Console.WriteLine("==================================");
    Console.WriteLine("1 - Adicionar nova ação");
    Console.WriteLine("2 - Atualizar todas as ações");
    Console.WriteLine("0 - Sair");
    Console.WriteLine("==================================");
    Console.Write("Escolha uma opção: ");

    string opcao = Console.ReadLine()?.Trim() ?? "";

    switch (opcao)
    {
        case "1":
            await AdicionarNovaAcaoAsync();
            break;

        case "2":
            await AtualizarTodasAsync();
            break;

        case "0":
            Console.WriteLine("\nEncerrando o InvestMoi Bot...");
            return;

        default:
            Console.WriteLine("\nOpção inválida!");
            break;
    }
}

// ADICIONAR NOVA AÇÃO
async Task AdicionarNovaAcaoAsync()
{
    Console.Write("\nDigite o código da ação: ");

    string codigo = Console.ReadLine()?.Trim().ToUpperInvariant() ?? "";

    if (string.IsNullOrWhiteSpace(codigo))
    {
        Console.WriteLine("Código inválido!");
        return;
    }

    try
    {
        await ProcessarAcaoAsync(codigo);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nErro ao consultar {codigo}: {ex.Message}");
        return;
    }

    try
    {
        await AtualizarIndexAsync(codigo);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nA ação foi salva, mas houve erro ao atualizar o índice: {ex.Message}");
    }
}

// ATUALIZAR TODAS AS AÇÕES DO INDEX
async Task AtualizarTodasAsync()
{
    try
    {
        List<string> acoes = await LerIndexAsync();

        if (acoes.Count == 0)
        {
            Console.WriteLine("\nNenhuma ação cadastrada no index.json.");
            return;
        }

        Console.WriteLine($"\nEncontradas {acoes.Count} ações para atualizar.");

        int sucesso = 0;
        int erros = 0;

        foreach (string codigo in acoes)
        {
            try
            {
                await ProcessarAcaoAsync(codigo);
                sucesso++;
            }
            catch (Exception ex)
            {
                erros++;
                Console.WriteLine($"\nErro ao atualizar {codigo}: {ex.Message}");
            }

            await Task.Delay(2000);
        }

        Console.WriteLine("\n==================================");
        Console.WriteLine("       ATUALIZAÇÃO FINALIZADA");
        Console.WriteLine("==================================");
        Console.WriteLine($"Total: {acoes.Count}");
        Console.WriteLine($"Sucesso: {sucesso}");
        Console.WriteLine($"Erros: {erros}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nErro ao processar index.json: {ex.Message}");
    }
}

// PROCESSAR E SALVAR UMA AÇÃO
async Task ProcessarAcaoAsync(string codigo)
{
    Console.WriteLine($"\nConsultando {codigo}...");

    var acao = await scraper.BuscarAcaoAsync(codigo);

    string caminho = Path.Combine(pasta, $"{acao.Codigo}.json");

    string json = JsonSerializer.Serialize(acao, opcoesJson);

    await File.WriteAllTextAsync(caminho, json);

    Console.WriteLine($"{acao.Codigo} atualizado com sucesso!");
    Console.WriteLine($"Arquivo: {caminho}");
}

// ATUALIZAR INDEX.JSON
async Task AtualizarIndexAsync(string codigo)
{
    List<string> index = await LerIndexAsync();

    bool existe = index.Contains(
        codigo,
        StringComparer.OrdinalIgnoreCase);

    if (!existe)
    {
        index.Add(codigo);

        index = index
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        string jsonIndex = JsonSerializer.Serialize(index, opcoesJson);

        await File.WriteAllTextAsync(caminhoIndex, jsonIndex);

        Console.WriteLine($"Ação {codigo} adicionada ao index.json!");
    }
    else
    {
        Console.WriteLine($"Ação {codigo} já existe no index.json.");
    }
}

async Task<List<string>> LerIndexAsync()
{
    if (!File.Exists(caminhoIndex))
    {
        await File.WriteAllTextAsync(caminhoIndex, "[]");
        return new List<string>();
    }

    string conteudo = await File.ReadAllTextAsync(caminhoIndex);

    // Arquivo vazio ou contendo apenas espaços.
    if (string.IsNullOrWhiteSpace(conteudo))
    {
        await File.WriteAllTextAsync(caminhoIndex, "[]");
        return new List<string>();
    }

    try
    {
        return JsonSerializer.Deserialize<List<string>>(conteudo)
               ?? new List<string>();
    }
    catch (JsonException)
    {
        throw new Exception(
            "O index.json contém um JSON inválido. Verifique o arquivo antes de continuar.");
    }
}
