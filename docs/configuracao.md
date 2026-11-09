# Configuração

## `config.json`: cadastro das PMUs

O mesmo `config.json` serve ao detector de eventos e a este mapa. O mapa **só lê o array
`pmus`**. As outras seções (`rabbitmq`, `postgresql`, `output`, `status`, `detectors`) são do
detector e são ignoradas aqui.

```json
{
  "pmus": [
    {
      "id": "GPA_UFSC",
      "active": true,
      "label": "UFSC - Florianópolis SC",
      "kv_ll": 0.38,
      "latitude": -27.5999722,
      "longitude": -48.5175778,
      "bindings": { "A": "…", "B": "…", "C": "…", "FLAG": "…" }
    }
  ]
}
```

| Campo | Usado pelo mapa | Observação |
|---|---|---|
| `id` | sim | Deve ser igual ao `pmu_id` dos CSVs. |
| `label` | sim | Nome exibido. **Termine com a sigla do estado** (`… SC`): é dela que o app deduz a região (N, NE, CO, SE, S). |
| `kv_ll` | sim | Tensão de linha em kV (aparece na coluna "Tensão"). |
| `latitude`, `longitude` | sim | Graus decimais. **Sem coordenadas a PMU não aparece no mapa.** |
| `active`, `bindings` | não | Usados só pelo detector. |

O arquivo [`exemplos/caso-basico/config.json`](../exemplos/caso-basico/config.json) serve de
modelo. Ele traz as seções do detector com valores de mentira (`SEU_HOST`, `SUA_SENHA`).

> ⚠️ O `config.json` de produção tem credenciais reais. Mantenha-o fora do git (o
> `.gitignore` da raiz já ignora `/config.json`).

## Parâmetros ajustáveis na tela

Os parâmetros abaixo ficam salvos em `%AppData%\Mapa\settings.json`
([`AppSettings`](../src/Mapa.Core/Settings/AppSettings.cs)). Os valores entre parênteses são
o padrão.

### Escalas de cor (limites inferior e superior por modo)

| Modo | Tensão | Frequência |
|---|---|---|
| Quantidade | 10 / 20 eventos | 5 / 10 eventos |
| Indefinidos | 5 / 15 eventos | 3 / 8 eventos |
| Severidade, sub | 0,70 / 0,90 pu | 58,0 / 59,5 Hz |
| Severidade, sobre | 1,10 / 1,30 pu | 60,5 / 62,0 Hz |
| Duração, sub e sobre | 100 / 500 ms | 10 / 60 s |

### Agrupamento em ocorrências (Detalhamento dos Eventos)

| Parâmetro | Padrão | Significado |
|---|---|---|
| VTCD: Gap máx. | 500 ms | Distância máxima entre uma detecção e a anterior para ficarem no mesmo grupo. |
| VTCD: Range máx. | 1000 ms | Duração máxima do grupo inteiro, da primeira à última detecção. |
| Frequência: Janela | 2000 ms | Janela de tempo para juntar detecções de frequência. |
| Frequência: Mín. PMUs | 3 | Número mínimo de PMUs para considerar um evento sistêmico. |
