// See https://aka.ms/new-console-template for more information
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;

class Program
{
    static void Main()
    {
        Random rand = new Random();

        // Tworzymy listę lekarzy (3 osoby)
        List<Doctor> doctors = new List<Doctor>();
        for (int i = 0; i < 3; i++) doctors.Add(new Doctor(i));

        // Tworzymy listę pielęgniarek (5 osób)
        List<Nurse> nurses = new List<Nurse>();
        for (int i = 0; i < 5; i++) nurses.Add(new Nurse(i));

        // Tworzymy obiekt symulacji i uruchamiamy ją na 1000 minut
        Simulation sim = new Simulation(doctors, nurses, rand);
        sim.Run(1000); // 1000 minut symulacji
    }
}

class Patient
{
    public int Id { get; private set; }          // ID pacjenta
    public string Priority { get; private set; } // Priorytet (High, Medium, Low)
    public int ArrivalTime { get; private set; } // Czas przyjścia pacjenta do szpitala (w minutach)
    public int WaitTime { get; set; }             // Czas oczekiwania na obsługę (ustawiany w trakcie symulacji)
    public int ServiceTime { get; private set; } // Czas leczenia u lekarza (minuty)
    public int NurseTime { get; private set; }   // Czas triage u pielęgniarki (minuty)

    public Patient(int id, int time, Random rand)
    {
        Id = id;
        ArrivalTime = time;
        Priority = GetRandomPriority(rand);      // Losujemy priorytet pacjenta
        NurseTime = rand.Next(5, 16);            // Losujemy czas triage (5-15 minut)
        ServiceTime = rand.Next(10, 31);         // Losujemy czas leczenia (10-30 minut)
    }

    // Metoda losująca priorytet pacjenta z rozkładem: 50% High, 30% Medium, 20% Low
    private string GetRandomPriority(Random rand)
    {
        int p = rand.Next(100);
        return p < 50 ? "High" : (p < 80 ? "Medium" : "Low");
    }
}

class Doctor
{
    public int Id { get; private set; }
    public bool IsAvailable { get; set; } // Czy lekarz jest dostępny do leczenia
    public int TimeBusy { get; set; }     // Łączny czas pracy lekarza (w minutach)

    public Doctor(int id)
    {
        Id = id;
        IsAvailable = true; // Na start lekarz jest dostępny
    }

    // Metoda leczenia pacjenta
    public void TreatPatient(Patient patient)
    {
        Console.WriteLine($"[LEKARZ {Id}] leczy pacjenta {patient.Id} ({patient.Priority})");
        Thread.Sleep(patient.ServiceTime);   // Symulujemy czas leczenia (blokuje wątek)
        TimeBusy += patient.ServiceTime;     // Dodajemy czas pracy
        IsAvailable = true;                   // Lekarz jest znowu dostępny po leczeniu
    }
}

class Nurse
{
    public int Id { get; private set; }
    public bool IsAvailable { get; set; } // Czy pielęgniarka jest dostępna do triage
    public int TimeBusy { get; set; }     // Łączny czas pracy pielęgniarki

    public Nurse(int id)
    {
        Id = id;
        IsAvailable = true; // Na start dostępna
    }

    // Metoda triage pacjenta
    public void TriagePatient(Patient patient)
    {
        Console.WriteLine($"[PIELĘGNIARKA {Id}] triage pacjenta {patient.Id} ({patient.Priority})");
        Thread.Sleep(patient.NurseTime);    // Symulacja triage (blokuje wątek)
        TimeBusy += patient.NurseTime;      // Dodajemy czas pracy
        IsAvailable = true;                 // Pielęgniarka znowu dostępna
    }
}

class Simulation
{
    private List<Doctor> doctors;
    private List<Nurse> nurses;
    private List<Patient> patientQueue; // Kolejka pacjentów oczekujących na obsługę
    private Random rand;
    private int patientCounter = 0;     // Licznik pacjentów do nadawania ID

    // Listy przechowujące czasy oczekiwania wg priorytetu
    private List<int> waitHigh = new();
    private List<int> waitMedium = new();
    private List<int> waitLow = new();

    public Simulation(List<Doctor> doctors, List<Nurse> nurses, Random rand)
    {
        this.doctors = doctors;
        this.nurses = nurses;
        this.rand = rand;
        patientQueue = new List<Patient>();
    }

    // Metoda uruchamiająca symulację przez określoną liczbę minut
    public void Run(int totalMinutes)
    {
        for (int time = 0; time < totalMinutes; time++)
        {
            double lambda = GetLambda(time);           // Intensywność napływu pacjentów (pacjentów na godzinę)
            double probArrival = lambda / 60.0;        // Prawdopodobieństwo przyjścia pacjenta w danej minucie
            if (rand.NextDouble() < probArrival)
                patientQueue.Add(new Patient(patientCounter++, time, rand)); // Dodajemy nowego pacjenta

            UpdateAvailability();    // Losowo zmieniamy dostępność lekarzy i pielęgniarek
            AssignNurses(time);      // Przydzielamy pacjentów pielęgniarkom do triage
            AssignDoctors(time);     // Przydzielamy pacjentów lekarzom do leczenia

            if (time % 100 == 0) Report(time);  // Co 100 minut wypisujemy raport statusu
        }

        SaveToCSV(); // Na koniec zapisujemy statystyki do pliku CSV
    }

    // Metoda zwracająca intensywność napływu pacjentów w danej minucie (pacjentów na godzinę)
    double GetLambda(int time)
    {
        int hour = (time / 60) % 24;
        // Więcej pacjentów przychodzi między 16 a 22 (20/godz), w innych godzinach 10/godz
        return (hour >= 16 && hour < 22) ? 20 : 10;
    }

    // Aktualizujemy dostępność lekarzy i pielęgniarek losowo (symulacja ich wolnych/zajętych momentów)
    void UpdateAvailability()
    {
        foreach (var d in doctors) d.IsAvailable = rand.Next(2) == 0;
        foreach (var n in nurses) n.IsAvailable = rand.Next(2) == 0;
    }

    // Funkcja punktująca priorytet pacjenta (do sortowania)
    int PriorityScore(string p) => p == "High" ? 3 : (p == "Medium" ? 2 : 1);

    // Przydzielanie pacjentów pielęgniarkom do triage
    void AssignNurses(int time)
    {
        foreach (var nurse in nurses.Where(n => n.IsAvailable).ToList())
        {
            // Wybieramy pacjenta o najwyższym priorytecie i najwcześniejszym czasie przybycia
            var patient = patientQueue.OrderByDescending(p => PriorityScore(p.Priority))
                                      .ThenBy(p => p.ArrivalTime).FirstOrDefault();
            if (patient == null) break;
            patientQueue.Remove(patient);
            nurse.IsAvailable = false;
            patient.WaitTime = time - patient.ArrivalTime;  // Obliczamy czas oczekiwania
            RecordWait(patient);                             // Zapisujemy czas oczekiwania wg priorytetu
            nurse.TriagePatient(patient);                    // Pielęgniarka wykonuje triage
            patientQueue.Add(patient);                        // Pacjent po triage wraca do kolejki oczekujących na lekarza
        }
    }

    // Przydzielanie pacjentów lekarzom do leczenia
    void AssignDoctors(int time)
    {
        foreach (var doctor in doctors.Where(d => d.IsAvailable).ToList())
        {
            // Wybieramy pacjenta o najwyższym priorytecie i najwcześniejszym czasie przybycia
            var patient = patientQueue.OrderByDescending(p => PriorityScore(p.Priority))
                                      .ThenBy(p => p.ArrivalTime).FirstOrDefault();
            if (patient == null) break;
            patientQueue.Remove(patient);
            doctor.IsAvailable = false;
            doctor.TreatPatient(patient);  // Lekarz leczy pacjenta (blokująca operacja)
        }
    }

    // Zapisujemy czas oczekiwania pacjenta do odpowiedniej listy wg priorytetu
    void RecordWait(Patient p)
    {
        if (p.Priority == "High") waitHigh.Add(p.WaitTime);
        else if (p.Priority == "Medium") waitMedium.Add(p.WaitTime);
        else waitLow.Add(p.WaitTime);
    }

    // Raport statusu symulacji wypisywany co 100 minut
    void Report(int time)
    {
        Console.WriteLine($"\nStatus @ {time} min:");
        Console.WriteLine($"Kolejka: {patientQueue.Count}, Lekarze dostępni: {doctors.Count(d => d.IsAvailable)}, Pielęgniarki dostępne: {nurses.Count(n => n.IsAvailable)}");
        Console.WriteLine($"Średni czas oczekiwania [Wysoki]: {waitHigh.DefaultIfEmpty(0).Average():F2} min");
        Console.WriteLine($"Średni czas oczekiwania [Średni]: {waitMedium.DefaultIfEmpty(0).Average():F2} min");
        Console.WriteLine($"Średni czas oczekiwania [Niski]: {waitLow.DefaultIfEmpty(0).Average():F2} min");
    }

    // Zapisujemy statystyki czasów oczekiwania do pliku CSV
    void SaveToCSV()
    {
        using StreamWriter sw = new("statystyki.csv");
        sw.WriteLine("Priority,WaitTime");
        waitHigh.ForEach(w => sw.WriteLine($"High,{w}"));
        waitMedium.ForEach(w => sw.WriteLine($"Medium,{w}"));
        waitLow.ForEach(w => sw.WriteLine($"Low,{w}"));

        Console.WriteLine("\nPlik 'statystyki.csv' zapisany z wynikami.");
    }
}