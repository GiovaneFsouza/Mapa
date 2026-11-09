# Arquitetura

```
┌──────────────────────────── Mapa.App (WPF, net8.0-windows) ────────────────────────────┐
│ MainWindow ── ViewModels (MVVM, CommunityToolkit) ── Views                             │
│      │                                                                                  │
│      └── LeafletMapBridge ──► WebView2 ──► Web/map.html + map.js (Leaflet, OSM)         │
└──────────────────────────────────────┬─────────────────────────────────────────────────┘
                                       │ usa
┌──────────────────────────── Mapa.Core (net8.0, sem UI) ───────────────────────────────┐
│ Import ─► Filtering ─► Aggregation / Correlation ─► Scales                             │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

## Fluxo de dados

1. **Import**: [`ConfigJsonPmuReader`](../src/Mapa.Core/Import/ConfigJsonPmuReader.cs) lê as
   PMUs e [`CsvBdedReader`](../src/Mapa.Core/Import/CsvBdedReader.cs) lê os eventos da pasta
   BDED, gerando registros [`PmuEvent`](../src/Mapa.Core/Models/PmuEvent.cs).
2. **Filtering**: [`EventFilterService`](../src/Mapa.Core/Filtering/EventFilterService.cs)
   aplica período, região, tipo e integridade
   ([`FilterCriteria`](../src/Mapa.Core/Filtering/FilterCriteria.cs)).
3. **Aggregation**:
   - [`AggregationEngine`](../src/Mapa.Core/Aggregation/AggregationEngine.cs) resume os
     eventos por PMU (quantidade, pior severidade, maior duração);
   - [`RegionalAggregationEngine`](../src/Mapa.Core/Aggregation/RegionalAggregationEngine.cs)
     resume por região;
   - [`OccurrenceCorrelator`](../src/Mapa.Core/Aggregation/OccurrenceCorrelator.cs) junta
     detecções próximas no tempo em ocorrências.
4. **Correlation**: [`FrequencyCorrelationEngine`](../src/Mapa.Core/Correlation/FrequencyCorrelationEngine.cs)
   agrupa eventos de frequência vistos por várias PMUs.
5. **Scales**: [`ThresholdScale`](../src/Mapa.Core/Scales/ThresholdScale.cs),
   [`LinearColorScale`](../src/Mapa.Core/Scales/LinearColorScale.cs) e
   [`MarkerVisualBuilder`](../src/Mapa.Core/Scales/MarkerVisualBuilder.cs) transformam números
   em cor e tamanho de marcador.
6. **App**: os ViewModels montam os payloads
   ([`MapPayloads`](../src/Mapa.App/Services/MapPayloads.cs)), e o `LeafletMapBridge` envia
   esses payloads como JSON para o `map.js` desenhar.

O `MainViewModel` observa a pasta BDED com um `FileSystemWatcher`. Quando algum CSV muda,
espera 1,5 s (para juntar gravações seguidas) e recarrega.

## Projetos

| Projeto | Alvo | Dependências |
|---|---|---|
| `Mapa.Core` | `net8.0` | ClosedXML, CommunityToolkit.Mvvm |
| `Mapa.App` | `net8.0-windows` (WPF) | Mapa.Core, Microsoft.Web.WebView2, CommunityToolkit.Mvvm |
| `Mapa.Core.Tests` | `net8.0` | xUnit |

Toda a regra de negócio fica no `Mapa.Core`, que pode ser testado sem interface gráfica. O
`ExampleDatasetTests` confere que o caso exemplo continua sendo lido sem erros.

## Código legado

`ExcelEventReader`, `XmlPmuReader` e `XmlFolderScanner` são do formato antigo (planilha
Excel + base XML de PMUs). Eles continuam no Core e nos testes, mas o app atual usa
BDED + `config.json`.
