# The IRS report

How the **IRS report** pages (`/tax` for dividends, `/tax/gains` for capital gains) get their figures, the rules it follows, and what is still to confirm. The report prepares figures to check. **It is not tax advice.** Before relying on it, check each year's form and the instructions of the Portal das Finanças.

Researched on 2026-10-13. Sources are at the end. When a rule changes, update this file and `Core/Tax/IrsReport.cs` together.

## What it produces

For a tax year:

- **Anexo J, quadro 8A** (rendimentos de capitais obtidos no estrangeiro): one line per income code and source country, numbered from 801:

  | Column | From |
  |---|---|
  | Código do rendimento | **E11** for dividends without withholding in Portugal |
  | País da fonte | ISO 3166 numeric code (Tabela X): 840 USA, 372 Ireland, 826 UK… |
  | Rendimento bruto | Sum of the gross dividends, in euros |
  | Imposto pago no estrangeiro (no país da fonte) | Sum of the tax withheld abroad, in euros, minus this year's withholding refunds when the switch is on |

  The columns for the paying agent's country (Savings Directive) and for tax withheld in Portugal do not apply to Trading 212 dividends and are left out.
- **Portuguese dividends:** listed apart, not in Anexo J (see below).
- **Interest and share lending:** listed apart, with a *suggested* code and country (see "To confirm").
- **Withholding refunds** ("Dividend adjustment") for the year.
- **Every dividend**, with the exchange rate used, gross, withheld and net in euros, and a check column. The grid exports to CSV, and the page prints.

## How each figure is computed

| Figure | Source and rule |
|---|---|
| **Gross, original currency** | shares × gross dividend per share. The API's `grossAmountPerShare` is the gross. A CSV report's "Price / share" is the **net** per share (checked on 2026-10-13: Apple May 2024 shows 0.2125 = 0.25 × 0.85). For rows seen only in a CSV report, the gross is net + withholding per share, flagged "gross derived". |
| **Tax withheld, original currency** | The most exact source first: shares × (API gross per share − CSV net per share). Else the CSV's "Withholding tax", which is rounded to cents, so tiny positions read 0.00. Else gross − net in euros, flagged "withholding estimated". |
| **Euros** | ECB euro reference rate of the payment date. Without one (weekend, holiday), the last quote before it, up to 7 days back. Rates are stored in `ExchangeRates` and refreshed after each sync, or with "Update exchange rates". |
| **Pence** | GBX amounts are pounds ÷ 100, since the ECB has no pence rate. |
| **Source country** | The instrument's override (set on its page), else the ISIN's first two letters. International ISINs ("XS…") are flagged until a country is set. |
| **Rate withheld** | Withheld ÷ gross in the original currency, so cents of rounding do not distort small dividends. |

Dividends are left out of the lines when:
- they are a **return of capital** (the type contains "return of capital"): it is not income;
- they have **no exchange rate**: the missing rates are listed at the top.

## Rules and where they come from

- **E11 and quadro 8A.** Foreign dividends paid through a foreign broker go in Anexo J, quadro 8A, with code E11, at the gross amount, with the foreign tax. Source: AT binding ruling, Proc. 26057 (2024-05-22). The tax rate is 28% unless the income is aggregated (quadro 8B).
- **Form.** Portaria n.º 104/2026/1 (2026-03-05) approves the current Anexo J for income of 2015 onwards. Its changes do not touch quadro 8A.
- **Exchange rate (Código do IRS, art. 23.º).**
  - Income from abroad uses the "câmbio de compra" of the date it was paid or made available (n.º 1 b).
  - Without a quote on that date, the last quote before it (n.º 3).
  - The article names no source for the rate. The ECB reference rate is the usual choice.
- **Credit for foreign tax (art. 81.º, n.º 2).** It cannot exceed the tax due under the double taxation treaty, typically 15% on US dividends. The page warns when a country withheld more than 15.5%.
- **One line per code and country.** This is the usual practice, and it fits the form.

## Portuguese dividends

- **What T212 reports:** Portuguese companies' dividends arrive with **35%** withheld in Portugal. That is the rate for omnibus accounts, where the beneficial owner is not identified, instead of the usual 28%.
- **Why they are apart:** they are not foreign income, so they are not in Anexo J. The page lists them separately.
- **Declaring:** optional, in Anexo E, if the income is aggregated.
- **Recovering the 7 points above 28%:** a question for a tax adviser.

## To confirm

- **Interest on cash and share lending.**
  - Code: E21 (interest without Portuguese withholding) is the likely one; share lending could also be E22. No official guidance was found.
  - Country: the country of the Trading 212 entity that holds the account. Most EU clients have been with Trading 212 Markets Ltd (Cyprus, 196). New Portuguese clients are onboarded with Trading 212 EU GmbH (Germany, 276) since about September 2026. Check the client agreement; the page lets you choose.
- **Withholding above the treaty rate:** whether to declare the full amount or the treaty-capped amount. The page shows what was withheld and warns.
- **Refunds:** deducting a year's US withholding refunds from that year's tax paid is the default, and can be switched off. Refunds that arrive after the return is filed may need a correction instead.
- **ADRs:** the source country is presumably the underlying company's, not the US. Set it per instrument.
- **Irish-domiciled ETFs:** Irish-source (372), usually with nothing withheld.

## Capital gains: Anexo J, quadro 9.2A

The **Capital gains** tab (`/tax/gains`) prepares the sales of shares and ETFs. Open positions are not declared: only sales are taxed.

**What it produces:** one line per sale and purchase it was matched to, numbered from 951. Each line carries:

| Column | From |
|---|---|
| País da fonte | The ISIN country, or the instrument's override. For ETFs, the fund's domicile, e.g. Ireland. |
| Código | **G01** for shares; **G20** for ETFs (fund units) |
| Realização: data, valor | Sale date; quantity × price, in euros |
| Aquisição: data, valor | Purchase date; quantity × price, in euros |
| Despesas e encargos | The broker's fees on the purchase (pro rata) and the sale |
| Imposto pago no estrangeiro | 0: nothing is withheld on these sales |
| País da contraparte | The country of the Trading 212 entity |
| Valores admitidos à negociação | Sim |

**How it is computed:**
- **FIFO (art. 43.º n.º 8 d)):** each sale takes the shares bought longest ago. The rule applies per institution (n.º 9); the page works per account, or across all of them.
- **Euros (art. 23.º):** each purchase and each sale is converted at the ECB rate of its own date.
  - The article says income uses the buying rate and expenses the selling rate. The ECB reference rate is a single middle rate.
  - Converting the acquisition at its own date is common practice; no ruling says so explicitly.
- **Expenses (art. 51.º n.º 1 b)):** fees "inerentes à aquisição e alienação". The currency conversion fees Trading 212 charges are counted. No ruling names them.
- **Splits (art. 43.º n.º 8 a)):** the new shares keep the original purchase date and cost.
  - Trading 212 records no order for a split, so splits are entered on the page (date, new shares per share).
  - When sales exceed purchases, the page names the instrument and the date where it happens. The uncovered quantity shows as a line with no acquisition value, so the sale value stays complete.
- **Trading 212's own result** is shown to compare. It uses average cost and its own exchange rate, so it differs.

**Taxation, for reference:**
- **Rate:** 28% on the year's balance (art. 72.º n.º 1 c)), or aggregation (englobamento) by choice (quadro 9.2C).
- **Mandatory aggregation (art. 72.º n.º 14):** a positive balance from assets held less than 365 days must be aggregated when taxable income, including it, reaches the top bracket. The top bracket starts at €83,696 for 2025 income and €86,634 for 2026.
- **Losses** can be carried forward 5 years only with aggregation (art. 55.º).
- **Partial exclusion (art. 43.º n.º 5, since 29 June 2024):** for listed securities and open-ended funds, 10% of the gain is excluded for holdings of 2 to 5 years, 20% for 5 to 8, and 30% from 8. The page shows the percentage per line. The form asks whether the securities are listed, and the AT should apply it.

**To confirm:**
- **The ETF code.** G20 fits the wording best. The OCC guide also uses G90, and some use G01. All are taxed alike: pick one and keep it.
- **The inflation coefficient (art. 50.º)** for holdings of more than 24 months: Anexo J's instructions are silent. The coefficients are about 1.00.
- **Spin-offs, mergers and scrip dividends:** not handled. The page lists those fills, if any, to check by hand.

## Anexo J, quadro 11: accounts abroad

- **What it is:** securities and deposit accounts at institutions outside Portugal are identified **every year, even without income** (AT FAQ 3808).
- **How to fill it for Trading 212:** there is no IBAN, so the account number goes in "Outros números de identificação". The tax pages show each account's number, which comes from the API's account summary, and the country of the Trading 212 entity.
- **Which entity:** most EU clients have been with Trading 212 Markets Ltd (Cyprus, 196). Check the client agreement.

## Sources

- AT binding ruling, Proc. 26057: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/informacoes_vinculativas/rendimento/cirs/Documents/PIV_26057.pdf
- Código do IRS, art. 23.º: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/Pages/irs23.aspx
- Código do IRS, art. 81.º: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/Pages/irs81.aspx
- Portaria n.º 104/2026/1: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/legislacao/diplomas_legislativos/Documents/portaria-104-2026-1.pdf
- OCC, "IRS – Rendimentos de 2025" (quadro 8A columns and Tabela V codes): https://www.occ.pt/sites/default/files/public/2026-03/Essencial_IRS2026_DIG_final.pdf
- OCC, "Guia Prático Mais-Valias" (2025): https://www.occ.pt/sites/default/files/public/2025-04/Guia_Pratico_MAIS-VALIASMd2.pdf
- Código do IRS, art. 43.º (FIFO, splits, partial exclusion): https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/Pages/irs43.aspx (same pattern for irs50, irs51, irs55, irs72)
- Lei 31/2024 (partial exclusion): https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/legislacao/diplomas_legislativos/Documents/Lei_31_2024.pdf
- AT FAQ on foreign income and accounts: https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/questoes_frequentes/Pages/faqs-00653.aspx
- ECB data API: https://data.ecb.europa.eu/help/api/data. Series `EXR/D.{CUR}.EUR.SP00.A`, `format=csvdata`, units of currency per euro, no rates on TARGET holidays.
