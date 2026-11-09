# Caso exemplo: junho e julho de 2026

São 12 PMUs de baixa tensão da rede MedFasee (campi universitários) e 75 eventos
**sintéticos**. Os eventos foram inventados para demonstrar o programa e não são medições
reais. Para gerar de novo: `python tools/gerar_exemplo.py`.

## Como abrir

1. `dotnet run --project src/Mapa.App`
2. **Histórico**: selecione a pasta `exemplos/caso-basico/bded`.
3. **Configuração**: selecione o arquivo `exemplos/caso-basico/config.json`.
4. **Período**: escolha de 01/06/2026 a 31/07/2026.

## O que tem nos dados

| # | Quando | O quê | Onde ver |
|---|---|---|---|
| 1 | 10/06 14:32:05 | **Afundamento regional no Sul** (AMT nas 3 fases). Começa na UFSC (0,62 pu) e chega a UTFPR, UFRGS e PTI em até ~60 ms. | Detalhamento → VTCD. A UFSC aparece com ★ (primeira detecção). |
| 2 | 10/06 18:07:12 | **Subfrequência sistêmica**: as 12 PMUs registram ~59,62 Hz por ~14 s. | Detalhamento → Desvio de Frequência. Ocorrência com 12 PMUs. |
| 3 | 15/06 03:41:50 | **Sobrefrequência no Norte/Nordeste**: ~60,3 Hz em 5 PMUs (UFPE, UFC, UFPA, UFAM, UNB). | Frequência, modo Severidade. |
| 4 | 20/06 09:15:30 | **Elevação de tensão** (EMT) na UNICAMP (1,18 pu) e na UFMG. | Tensão, sentido "sobre". |
| 5 | 22/06 16:02:08 | **Interrupção momentânea** (IMT) na UFMT, ~0,9 s. | Modo Duração. |
| 6 | 25/06 11:48:03 | Evento **UNDEFINED** na UNB, com qualidade `00000101` (início incerto + frames inválidos). | Modo Indefinidos (os outros modos não mostram `UNDEFINED`). |
| — | jun–jul | 40 afundamentos isolados espalhados, para dar volume aos modos Quantidade e Ranking. | Modo Quantidade, Ranking. |

## Arquivos

```
caso-basico/
  config.json              12 PMUs com coordenadas; credenciais de mentira
  bded/<pmu>/2026/06.csv   eventos de junho
  bded/<pmu>/2026/07.csv   eventos de julho
```
