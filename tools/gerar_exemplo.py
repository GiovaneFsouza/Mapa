"""Gera o caso exemplo sintético (config.json + pasta BDED) em exemplos/caso-basico.

Os eventos são inventados (semente fixa, sempre o mesmo resultado); as PMUs e coordenadas
são as dos campi da rede MedFasee. Uso:  python tools/gerar_exemplo.py [pasta_destino]
"""
import json, os, random, shutil, sys
from datetime import datetime, timedelta

random.seed(42)
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "exemplos", "caso-basico")

pmus = [
    ("GPA_UFSC", "UFSC - Florianópolis SC", 0.38, -27.5999722, -48.5175778),
    ("GPA_UTFPR", "UTFPR - Curitiba PR", 0.22, -25.4395972, -49.2687333),
    ("GPA_UFRGS", "UFRGS - Porto Alegre RS", 0.22, -30.02, -51.22),
    ("GPA_PTI", "PTI - Foz do Iguaçu PR", 0.22, -25.435919, -54.597089),
    ("GPA_UNICAMP", "UNICAMP - Campinas SP", 0.22, -22.821167, -47.066207),
    ("GPA_UFMG", "UFMG - Belo Horizonte MG", 0.22, -19.8695111, -43.9618611),
    ("GPA_UNB", "UNB - Brasília DF", 0.38, -15.7659667, -47.8720889),
    ("GPA_UFMT", "UFMT - Cuiabá MT", 0.22, -15.6078833, -56.0635139),
    ("GPA_UFPE", "UFPE - Recife PE", 0.38, -8.0531167, -34.9545667),
    ("GPA_UFC", "UFC - Fortaleza CE", 0.38, -3.7437667, -38.5767611),
    ("GPA_UFPA", "UFPA - Belém PA", 0.22, -1.4742306, -48.4519611),
    ("GPA_UFAM", "UFAM - Manaus AM", 0.22, -3.0883972, -59.9633056),
]

config = {
    "rabbitmq": {"host": "SEU_HOST", "port": 5552, "user": "SEU_USUARIO", "password": "SUA_SENHA", "stream": "stream_gen"},
    "postgresql": {"host": "localhost", "port": 5432, "database": "bded_db", "user": "SEU_USUARIO", "password": "SUA_SENHA"},
    "output": {"bded_dir": "bded", "enable_csv": True, "enable_postgresql": False, "debug_pmu": "",
               "enable_vtcd": True, "enable_frequency": True},
    "status": {"enabled": True, "interval_minutes": 1},
    "detectors": {
        "vtcd": {"timeout_ms": 60000, "bands": {"default": {"lower_pu": 0.9, "upper_pu": 1.1},
                                                 "kv500": {"lower_pu": 0.945, "upper_pu": 1.155}}},
        "frequency": {"ema_alpha": 0.095, "entry_low_hz": 59.8, "entry_high_hz": 60.2, "exit_low_hz": 59.9,
                      "exit_high_hz": 60.1, "min_duration_ms": 100, "timeout_ms": 60000},
    },
    "pmus": [
        {"id": i, "active": True, "label": l, "kv_ll": kv, "latitude": la, "longitude": lo,
         "bindings": {"A": f"{i}:VA.MAG", "B": f"{i}:VB.MAG", "C": f"{i}:VC.MAG", "FREQ": f"{i}:FREQ", "FLAG": f"{i}:FLAG"}}
        for i, l, kv, la, lo in pmus
    ],
}
os.makedirs(out, exist_ok=True)
with open(os.path.join(out, "config.json"), "w", encoding="utf-8") as f:
    json.dump(config, f, ensure_ascii=False, indent=2)

rows = []  # (pmu, source_id, type, start datetime, duration_ms, extreme, quality)

def add(pmu, src, typ, start, dur_ms, ext, q="00000000"):
    rows.append((pmu, src, typ, start, dur_ms, ext, q))

D = datetime
# 1) Afundamento regional no Sul (VTCD, 4 PMUs, poucos ms de diferença)
t0 = D(2026, 6, 10, 14, 32, 5, 120000)
for k, (p, dt_ms, ext) in enumerate([("GPA_UFSC", 0, 0.62), ("GPA_UTFPR", 18, 0.71), ("GPA_UFRGS", 35, 0.78), ("GPA_PTI", 61, 0.86)]):
    for ph in (2, 3, 4):
        add(p, ph, "AMT", t0 + timedelta(milliseconds=dt_ms + ph), 80 + 20 * k + ph * 3, round(ext + 0.01 * (ph - 2), 4))

# 2) Subfrequência sistêmica (todas as PMUs, ~2 s de janela)
t1 = D(2026, 6, 10, 18, 7, 12, 0)
for k, (p, *_rest) in enumerate(pmus):
    add(p, 1, "Underfrequency", t1 + timedelta(milliseconds=40 * k + random.randint(0, 30)),
        14000 + random.randint(-1500, 1500), round(59.62 + random.uniform(-0.03, 0.03), 3))

# 3) Sobrefrequência no Norte/Nordeste (5 PMUs)
t2 = D(2026, 6, 15, 3, 41, 50, 500000)
for k, p in enumerate(["GPA_UFPE", "GPA_UFC", "GPA_UFPA", "GPA_UFAM", "GPA_UNB"]):
    add(p, 1, "Overfrequency", t2 + timedelta(milliseconds=60 * k), 6000 + 300 * k, round(60.31 - 0.02 * k, 3))

# 4) Elevação de tensão local (Sudeste, 2 PMUs)
t3 = D(2026, 6, 20, 9, 15, 30, 250000)
add("GPA_UNICAMP", 2, "EMT", t3, 140, 1.18)
add("GPA_UFMG", 3, "EMT", t3 + timedelta(milliseconds=45), 110, 1.12)

# 5) Interrupção momentânea isolada (Centro-Oeste)
add("GPA_UFMT", 2, "IMT", D(2026, 6, 22, 16, 2, 8, 700000), 900, 0.05)
add("GPA_UFMT", 3, "IMT", D(2026, 6, 22, 16, 2, 8, 702000), 902, 0.06)
add("GPA_UFMT", 4, "IMT", D(2026, 6, 22, 16, 2, 8, 701000), 898, 0.05)

# 6) Evento indefinido com início incerto e frames inválidos (bits 0 e 2)
add("GPA_UNB", 4, "UNDEFINED", D(2026, 6, 25, 11, 48, 3, 30000), 420, 0.81, "00000101")

# 7) Ruído de fundo: afundamentos isolados ao longo de junho e julho
for _ in range(40):
    p = random.choice(pmus)[0]
    day = D(2026, random.choice([6, 7]), random.randint(1, 28), random.randint(0, 22), random.randint(0, 59),
            random.randint(0, 59), random.randint(0, 999) * 1000)
    add(p, random.choice([2, 3, 4]), "AMT", day, random.randint(30, 400), round(random.uniform(0.7, 0.89), 4))

bded = os.path.join(out, "bded")
shutil.rmtree(bded, ignore_errors=True)
files = {}
for pmu, src, typ, st, dur, ext, q in sorted(rows, key=lambda r: (r[0], r[3])):
    en = st + timedelta(milliseconds=dur)
    line = f"{pmu},{src},{typ},{st:%Y-%m-%d},{st:%H:%M:%S}.{st.microsecond // 1000:03d}," \
           f"{en:%H:%M:%S}.{en.microsecond // 1000:03d},{ext},{q}"
    files.setdefault(os.path.join(bded, pmu, f"{st:%Y}", f"{st:%m}.csv"), []).append(line)
for path, lines in files.items():
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("pmu_id,source_id,event_type,date,start_time,end_time,extreme_value,quality\n")
        f.write("\n".join(lines) + "\n")
print(len(rows), "eventos em", len(files), "arquivos")
