# Desktop app integration with DbBridge DLL
_Exported on 3/28/2026 at 14:46:16 GMT+2 from Cursor (2.6.19)_

---

**User**

This plan specific to desktop app. So I have example app in DbBridge folder. This and @DbBridge/DbBridge_DLL_lietosanas_instrukcija_v2.docx  which contents I paste below. 
I want my Desktop app to be able to use this DLL and to make the changes. I know that not all changes are possible yet, but do what you can and clearly show what this bridge cannot do. also look at @DbBridge/DbBridgeWin32CSharp/README.txt 

DbBridge DLL lietošanas instrukcija
Instrukcija kolēģim darbam ar DBISAM datubāzi no ārējas Win32 programmas (piemēram, C# x86).
Ātrais starts
Solis	Darbība	Piezīme
1	Izsauc DbOpen(DataDir)	Saņem CtxHandle vai NULL
2	Veic vajadzīgās nolasīšanas vai labojumus	Pēc kļūdas nolasīt DbGetLastError
3	Darba beigās izsauc DbClose(CtxHandle)	Aizver context un samazina engine refcount
Mērķis
DLL paredzēts drošam darbam ar DBISAM datubāzi no ārējas Win32 programmas.
Tipiski uzdevumi: atvērt DB mapi, nolasīt dalībnieku informāciju, atrast IdNr pēc StartNr vai ChipNr, labot starta laikus, ChipNr un KatNr, kā arī nolasīt kļūdas tekstu.
Svarīgi ierobežojumi
Vienā procesā DLL iekšpusē tiek izmantots viens globāls TDBISAMEngine. Tas ir singleton visam procesam.
Katram DbOpen izsaukumam ir savs context rokturis (CtxHandle), savs Session un savs Database objekts.
Vienmēr jāpabeidz darbs ar DbClose(CtxHandle).
Prasības lietošanai
Process jāpalaiž kā x86 / 32-bit.
Jābūt pieejamai DBISAM datu mapei un nepieciešamajiem runtime komponentiem.
DataDir jānorāda kā ceļš uz DB mapi, piemēram A:\DB.
Pamatrežīms
Normālā režīmā starta laiku un ChipNr drīkst labot tikai konkrētajā sacensību dienā.
Dienas atļauja tiek pārbaudīta pēc WKCfg laukiem WKNzDatum1..WKNzDatum6.
Ja nepieciešama testēšana, jāizmanto DbSetTestMode(CtxHandle, DayNo) un pēc tam DbDisableTestMode(CtxHandle).
Status kodi
Kods	Konstante	Nozīme
1	DBR_OK	Darbība izdevās.
0	DBR_ERROR	Vispārēja kļūda; jālasa DbGetLastError.
-1	DBR_NOT_FOUND	Ieraksts nav atrasts.
-2	DBR_INVALID_DAY	Nederīgs DayNo; atļauts 1..6.
-3	DBR_DAY_NOT_ALLOWED	Konkrētās dienas labojums nav atļauts.
-4	DBR_INVALID_TIME	Laiks nav hh:mm:ss formātā.
-5	DBR_CTX_NIL	Nederīgs vai tukšs context handle.
-6	DBR_MULTIPLE_MATCHES	Atrasti vairāki ieraksti tur, kur tika gaidīts viens.
Atvēršana un aizvēršana
DbOpen(DataDir) — Atver DBISAM datu mapi un atgriež CtxHandle vai NULL.
DbClose(CtxHandle) — Aizver context un atbrīvo resursus.
DbGetLastError(CtxHandle, Buffer, BufferSize) — Ieraksta pēdējo kļūdas tekstu buferī.
Konfigurācija un etapi
DbReloadConfig — Pārlasa WKCfg.
DbGetEtapInfo — Atgriež etapa nosaukumu, datumu un Nullzeit vērtību.
DbSetTestMode / DbDisableTestMode — Ieslēdz vai izslēdz testa režīmu konkrētai dienai.
Starta laika maiņa
DbChangeStartTimeByIdNr — Maina starta laiku pēc IdNr.
DbChangeStartTimeByStartNr — Maina starta laiku pēc StartNr.
DbChangeStartTimeByChipNr — Maina starta laiku pēc konkrētās dienas ChipNrN.
ChipNr un grupa
DbChangeChipNrByIdNr / ByStartNr / ByOldChipNr — Maina ChipNrN.
DbChangeKatNrByIdNr / ByStartNr / ByChipNr — Maina KatNr.
Nolasīšanas funkcijas
DbGetTeilnInfoByIdNr — Atgriež dalībnieka informāciju, tai skaitā Start1..3 raw un hh:mm:ss formā.
DbGetIdNrListByStartNr — Atgriež IdNr sarakstu pēc StartNr.
DbGetIdNrListByChipNr — Atgriež IdNr sarakstu pēc konkrētās dienas ChipNrN.
Drošība pret nepareiziem labojumiem
    • Ja meklēšana notiek pēc StartNr vai ChipNr, DLL pirms UPDATE pārbauda, cik rindas atbilst nosacījumam.
    • Ja nav atrasta neviena rinda, tiek atgriezts DBR_NOT_FOUND; ja atrastas vairākas rindas, DBR_MULTIPLE_MATCHES.
    • UPDATE tiek izpildīts transakcijā; ja rezultāts nav precīzi viena rinda, tiek veikts Rollback.
Log faili
    • Globālais DLL logs: DbBridge_dll.log. Tur tiek rakstīta Engine izveide, DbOpen, DbClose un refcount informācija.
    • DB līmeņa kļūdu logs: <DataDir>\logs\DbBridge_YYYYMMDD.log. Tur tiek rakstīts SQL, parametri, renderētais SQL un kļūdas teksts.
    • Ja rodas problēmas ar lock, vispirms jāpārbauda abi log faili.
Ieteicamais darba modelis C# pusē
    • Pēc DbOpen jāpārbauda, vai saņemtais rokturis nav IntPtr.Zero.
    • Pēc katras neveiksmīgas DLL funkcijas jāizsauc DbGetLastError un jāieraksta kļūda savā logā.
    • CtxHandle vienmēr jāaizver finally blokā ar DbClose.
Lokošana un bloķēšanas pārbaude
DLL izmanto DBISAM bloķēšanas mehānismu ar šādiem Engine iestatījumiem:
    • GEngine.LockFileName := 'sportsw.sklc';
    • GEngine.LargeFileSupport := False;
Praktiskā nozīme. Lock fails 'sportsw.sklc' nodrošina, ka visas programmas instances strādā ar vienotu DBISAM bloķēšanas informāciju. Parametrs LargeFileSupport ir iestatīts uz False, lai DLL darbotos ar to pašu bloķēšanas režīmu, ko izmanto oriģinālā programma.
Kā pārbaudīt lokošanu. Atver oriģinālajā programmā kādu dalībnieka ierakstu labošanas režīmā un neatstāj šo režīmu. Pēc tam ar testa programmu mēģini tieši tam pašam ierakstam izpildīt labojumu, piemēram, nomainīt starta laiku, chip numuru vai citu lauku.
Sagaidāmais rezultāts. Ja oriģinālā programma konkrēto ierakstu tur atvērtu rediģēšanai, atkārtota rediģēšana no DLL puses netiks veikta. Parasti tiks saņemta DBISAM kļūda par to, ka ierakstu nevar nobloķēt. Tas ir pareizs un drošs rezultāts — DLL nedrīkst pārrakstīt ierakstu, kuru pašlaik rediģē cita programma vai cita sesija.
Piezīme. Ja bloķēšana ir tikai ļoti īslaicīga, DBISAM pats var mēģināt operāciju atkārtot atbilstoši saviem iekšējiem lock gaidīšanas un retry mehānismiem. Tāpēc īsi konflikti var tikt atrisināti automātiski, bet ilgāka rediģēšanas bloķēšana paliks kā kļūda un izmaiņas netiks saglabātas.
Piemērs no pārbaudes. Zemāk redzams ieraksts, kas oriģinālajā programmā ir atvērts rediģēšanai; šādā brīdī testa programmai, mēģinot labot to pašu ierakstu, jāsaņem lock konflikts.

Attēls: ieraksts oriģinālajā programmā atvērts labošanas režīmā.




---

**Cursor**

Now let me read the existing desktop app files and the DbBridge example code to understand the current implementation and what needs to change.

Let me also look at the existing MainForm and the DbBridge example form to understand the UI integration pattern.

Now I have a thorough understanding of both the existing desktop app and the DbBridge DLL capabilities. Let me map out what the bridge can and cannot do relative to the current ODBC-based approach.

---

**User**

no , I cant use ODBC for DBISAM, right? does ODBC has support for this old legac database type? 

---

**Cursor**

You're right. DBISAM is a niche embedded Delphi database -- there's no readily available ODBC driver for it on modern systems. The `DBISAM 4 ODBC Driver` referenced in the current code was aspirational/placeholder. That's precisely why the **DbBridge DLL was created** -- it's the only practical way to access DBISAM from outside Delphi.

This fundamentally changes the plan. DbBridge is not a supplement to ODBC -- it's the **sole access method**. Let me revise the plan to reflect this, and clearly surface the gaps.

Before I do, one question:

**DbBridge output format**

> **Do you know what format DbGetTeilnInfoByIdNr returns in the buffer? (e.g. CSV, key=value pairs, pipe-delimited, etc.) And which fields it includes -- does it have Name, Surname, ChipNr, Class, Club, Country, StartPlace, DNS status?**
> - Yes, I know the format and can share it
> - No, I'd need to test the DLL to find out ✔
> - I can check with the DLL author / have source access

Got it -- we'll need to discover the format by actually calling the DLL. Let me revise the plan to reflect the DbBridge-only reality and add a format discovery step.

The plan is updated. Key changes from the previous version:

**The big picture shift:** DbBridge.dll is the **only** access method, not a supplement. All ODBC code gets replaced.

**What works now:**
- API-to-DBISAM writes: ChipNr, StartTime, KatNr changes from Android referees flow back into OE12
- Etap/stage info display
- Individual participant lookups
- Test mode for off-day testing

**What's blocked (DLL gaps):**
- **Bulk read all runners** -- no enumerate function, so DBISAM-to-API push can't work yet
- **DNS write-back** -- no function to set AufgabeTyp
- **Name/Surname/Club/Country write** -- no such functions

**What to ask the DLL author for:**
1. A `DbReadAllTeiln` or iterator function to enumerate all participants
2. A `DbChangeDnsByStartNr` function to set DNS status
3. Or ideally a raw `DbExecSQL` that would solve everything

The "Force Push All" button would be disabled until bulk read is available. The partial sync (PULL + write field changes) is still valuable and ready to build.

---

**User**

so just skip the functaionality that you can't implement - I can ask the Delphi developer to add this functionality.

---

**Cursor**

Understood. I'll implement everything DbBridge supports, throw `NotSupportedException` for the gaps (bulk read, DNS write), and leave clear markers for the Delphi developer. Ready to execute when you confirm.

---

**User**

so for now, I also want to add buttons that can manually test all the provided functionality, so I see how the read/write works, and also explore that actual table formats/data that comes back... and I think that I have to be able to open/close DB and select which day from multi-day event I am working with.

---

**Cursor**

Good thinking -- exploring the raw data first before building sync logic on top. Let me update the plan to prioritize a testing/exploration UI.

Good idea. Let me update the plan to add the testing/exploration UI as the primary deliverable -- it'll let you probe the data formats and test all DLL functions before building sync logic.

Plan updated. The main addition is **Step 4: DbBridge Explorer Form** -- a standalone dialog you can open from MainForm to manually test every DLL function:

- **Open/Close DB** with path browse
- **Day selector** (1-6) that persists across operations
- **Read section**: Get Etap Info, Get Teiln Info by IdNr, Get IdNr List by StartNr/ChipNr
- **Write section**: Change StartTime, ChipNr, KatNr -- each with by-IdNr / by-StartNr / by-ChipNr variants
- **Test Mode** toggle
- **Raw output log** showing verbatim buffer contents from the DLL -- this is how you'll discover the `DbGetTeilnInfoByIdNr` format

The explorer form is the priority. Once you've explored the data formats and confirmed everything works, the sync integration (steps 6-7) builds on top of it.

Ready to execute when you confirm.

