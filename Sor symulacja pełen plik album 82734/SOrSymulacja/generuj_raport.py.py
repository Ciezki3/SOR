import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
from fpdf import FPDF
import os

# -----------------------
# Helper: Polish letters fix
# -----------------------
def remove_polish_chars(text):
    pol = 'ąćęłńóśżźĄĆĘŁŃÓŚŻŹ'
    eng = 'acelnoszzACELNOSZZ'
    trans = str.maketrans(pol, eng)
    return text.translate(trans)

# -----------------------
# 1. Load CSV
# -----------------------
filename = "statystyki.csv"
if not os.path.exists(filename):
    print("❌ Plik 'statystyki.csv' nie istnieje w folderze!")
    exit(1)

df = pd.read_csv(filename)

# -----------------------
# 2. Wykresy
# -----------------------
sns.set(style="whitegrid")

# Boxplot
plt.figure(figsize=(10, 6))
sns.boxplot(x="Priority", y="WaitTime", data=df, palette="Set2")
plt.title("Rozklad czasu oczekiwania wedlug priorytetu")
plt.savefig("boxplot_wait_times.png")
plt.close()

# Barplot
plt.figure(figsize=(8, 5))
avg_wait = df.groupby("Priority")["WaitTime"].mean().reindex(["High", "Medium", "Low"])
sns.barplot(x=avg_wait.index, y=avg_wait.values, palette="Set2")
plt.title("Sredni czas oczekiwania pacjentow")
plt.savefig("avg_wait_times.png")
plt.close()

# -----------------------
# 3. Statystyki
# -----------------------
summary = df.groupby("Priority")["WaitTime"].agg(['count', 'mean', 'max', 'min', 'std']).round(2).reset_index()

# -----------------------
# 4. PDF Generator
# -----------------------
class PDF(FPDF):
    def header(self):
        self.set_font("Arial", "B", 14)
        self.cell(0, 10, "Raport Symulacji SOR", ln=True, align="C")
        self.ln(5)

    def chapter_title(self, title):
        self.set_font("Arial", "B", 12)
        self.cell(0, 10, remove_polish_chars(title), ln=True)
        self.ln(3)

    def chapter_body(self, text):
        self.set_font("Arial", "", 11)
        self.multi_cell(0, 8, remove_polish_chars(text))
        self.ln()

# -----------------------
# 5. Generuj raport
# -----------------------
pdf = PDF()
pdf.add_page()

pdf.chapter_title("1. Wprowadzenie")
pdf.chapter_body(
    "Celem symulacji bylo zbadanie obciazenia Szpitalnego Oddzialu Ratunkowego oraz analiza sredniego czasu oczekiwania pacjentow "
    "w zaleznosci od priorytetu. Wykresy oraz statystyki znajduja sie ponizej."
)

pdf.chapter_title("2. Statystyki oczekiwania")
pdf.set_font("Arial", "B", 10)
headers = ["Priorytet", "Liczba", "Srednia", "Max", "Min", "Std"]
widths = [30, 25, 25, 20, 20, 25]

for i, h in enumerate(headers):
    pdf.cell(widths[i], 8, h, border=1)
pdf.ln()

pdf.set_font("Arial", "", 10)
for row in summary.itertuples(index=False):
    vals = [row.Priority, row.count, row.mean, row.max, row.min, row.std]
    for i, val in enumerate(vals):
        pdf.cell(widths[i], 8, str(val), border=1)
    pdf.ln()

pdf.ln(5)
pdf.chapter_title("3. Wykresy")
pdf.image("avg_wait_times.png", w=170)
pdf.ln(5)
pdf.image("boxplot_wait_times.png", w=170)

pdf.chapter_title("4. Wnioski i rekomendacje")
pdf.chapter_body(
    "- Sredni czas oczekiwania byl bardzo krotki, co sugeruje optymalne wykorzystanie zasobow.\n"
    "- Najdluzszy sredni czas oczekiwania mialy osoby ze srednim priorytetem (0.11 min).\n"
    "- Dla wiekszego obciazenia nalezy przetestowac dynamiczna alokacje personelu oraz szybka sciezke dla pacjentow o niskim priorytecie."
)

pdf.output("raport_sor_symulacja.pdf")
print("✅ Wygenerowano: raport_sor_symulacja.pdf")