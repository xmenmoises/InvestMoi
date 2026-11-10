using System;
using System.Collections.Generic;
using System.Text;

namespace InvestMoiBot.Models
{
    public class AcaoFundamentalista
    {
        public string Codigo { get; set; } = "";
        public string Nome { get; set; } = "";

        public AnaliseBarsi Analise_Barsi { get; set; } = new();

        public AnaliseGraham Analise_Graham { get; set; } = new();

    }
}
