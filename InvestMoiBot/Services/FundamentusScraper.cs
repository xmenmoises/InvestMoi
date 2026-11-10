using HtmlAgilityPack;
using InvestMoiBot.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace InvestMoiBot.Services
{
    public class FundamentusScraper
    {
        private readonly HttpClient _http;

        // Nomes amigáveis para evitar salvar descrições como "KLABIN PN N2".
        private static readonly Dictionary<string, string> NomesEmpresas = new()
        {
            ["KLBN4"] = "Klabin S.A."
        };

        public FundamentusScraper(HttpClient http)
        {
            _http = http;

            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; InvestMoiBot/1.0)");
        }

        public async Task<AcaoFundamentalista> BuscarAcaoAsync(string codigo)
        {
            codigo = codigo.Trim().ToUpperInvariant();

            // 1. Busca os dados da página de detalhes.
            string urlDetalhes =
                $"https://www.fundamentus.com.br/detalhes.php?papel={codigo}";

            string htmlDetalhes = await _http.GetStringAsync(urlDetalhes);

            var docDetalhes = new HtmlDocument();
            docDetalhes.LoadHtml(htmlDetalhes);

            decimal lpa = BuscarIndicador(docDetalhes, "LPA");
            decimal vpa = BuscarIndicador(docDetalhes, "VPA");

            // O Fundamentus apresenta a descrição do papel, não necessariamente
            // a razão social amigável da empresa.
            string nome = NomesEmpresas.TryGetValue(codigo, out var nomeConfigurado)
                ? nomeConfigurado
                : BuscarIndicadorTexto(docDetalhes, "Empresa");

            // 2. Busca os proventos.
            string urlProventos =
                $"https://www.fundamentus.com.br/proventos.php?papel={codigo}&tipo=2";

            string htmlProventos = await _http.GetStringAsync(urlProventos);

            var docProventos = new HtmlDocument();
            docProventos.LoadHtml(htmlProventos);

            var dividendos = BuscarDividendosAgrupados(docProventos);

            // 3. Monta somente os campos necessários.
            return new AcaoFundamentalista
            {
                Codigo = codigo,
                Nome = nome,

                Analise_Barsi = new AnaliseBarsi
                {
                    Dividendos = dividendos
                },

                Analise_Graham = new AnaliseGraham
                {
                    LPA = lpa,
                    VPA = vpa
                }
            };
        }

        private static decimal BuscarIndicador(
            HtmlDocument doc,
            string indicador)
        {
            string valor = BuscarIndicadorTexto(doc, indicador);

            return ConverterDecimal(valor);
        }

        private static string BuscarIndicadorTexto(
            HtmlDocument doc,
            string indicador)
        {
            var labels = doc.DocumentNode.SelectNodes(
                "//td[contains(concat(' ', normalize-space(@class), ' '), ' label ')]");

            if (labels == null)
                throw new Exception("Não foram encontrados os indicadores da página.");

            foreach (var label in labels)
            {
                string nome = LimparTexto(label.InnerText)
                    .TrimStart('?', ' ');

                if (!nome.Equals(indicador, StringComparison.OrdinalIgnoreCase))
                    continue;

                var valorNode = label.SelectSingleNode("following-sibling::td[1]");

                if (valorNode == null)
                    break;

                return LimparTexto(valorNode.InnerText);
            }

            throw new Exception($"Indicador '{indicador}' não encontrado.");
        }

        private static Dictionary<string, decimal> BuscarDividendosAgrupados(
            HtmlDocument doc)
        {
            // Localiza a tabela que possui os cabeçalhos Ano e Valor.
            var tabelas = doc.DocumentNode.SelectNodes("//table");

            if (tabelas == null)
                throw new Exception("Nenhuma tabela encontrada na página de proventos.");

            HtmlNode? tabelaAnual = null;

            foreach (var tabela in tabelas)
            {
                var cabecalhos = tabela.SelectNodes(
                    ".//tr[1]/*[self::td or self::th]");

                if (cabecalhos == null)
                    continue;

                var nomes = cabecalhos
                    .Select(c => LimparTexto(c.InnerText))
                    .ToList();

                if (nomes.Count >= 2 &&
                    nomes[0].Equals("Ano", StringComparison.OrdinalIgnoreCase) &&
                    nomes[1].Equals("Valor", StringComparison.OrdinalIgnoreCase))
                {
                    tabelaAnual = tabela;
                    break;
                }
            }

            if (tabelaAnual == null)
                throw new Exception("Tabela de dividendos agrupados por ano não encontrada.");

            var dividendos = new Dictionary<string, decimal>();

            var linhas = tabelaAnual.SelectNodes(".//tr");

            if (linhas == null)
                return dividendos;

            foreach (var linha in linhas)
            {
                var celulas = linha.SelectNodes("./td");

                if (celulas == null || celulas.Count < 2)
                    continue;

                string ano = LimparTexto(celulas[0].InnerText);
                string valor = LimparTexto(celulas[1].InnerText);

                // Ignora cabeçalhos e linhas que não representam anos.
                if (!Regex.IsMatch(ano, @"^\d{4}$"))
                    continue;

                dividendos[ano] = ConverterDecimal(valor);
            }

            if (dividendos.Count == 0)
                throw new Exception("Nenhum dividendo anual foi encontrado.");

            return dividendos;
        }

        private static decimal ConverterDecimal(string valor)
        {
            valor = LimparTexto(valor);

            // Exemplos:
            // 0,339          -> 0.339
            // 1.234,56       -> 1234.56
            // 20.345.700.000 -> 20345700000

            valor = valor.Replace(".", "").Replace(",", ".");

            return decimal.Parse(
                valor,
                NumberStyles.Number,
                CultureInfo.InvariantCulture);
        }

        private static string LimparTexto(string texto)
        {
            return HtmlEntity.DeEntitize(texto)
                .Replace('\u00A0', ' ')
                .Trim();
        }
    }
}
