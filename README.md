# DSO MagicBox

> **Şirket içi bir çekirdekten doğdu, şimdi herkese açık.**
>
> **MagicBox**, değerleri *sıfır parametreli* `Func<T>` delege hücreleri olarak tutan, **DynamicInvoke kullanmadan** tür-güvenli okuma yapan, performans odaklı bir mini konteyner ve üzerine inşa edilmiş hafif bir **satır/tablolaştırma** katmanıdır.
>
> Ad alanı: `DSO.Core.MagicBox`

---

## ✨ Neler Sunuyor?
- **Tür-Güvenli Çekim:** `Get<T>()` / `TryGet<T>(out T)` ile doğru tipte veriyi **casting** veya **DynamicInvoke** olmadan alın.
- **Hızlı `object`/`string` Erişimi:** `TryGetVal(out object)` ve `GetString()` hot-path’te tip başına derlenen **invoker cache** kullanır.
- **Laziness ve Esneklik:** Hücreler `Func<T>` ile temsil edilir; gerçek değer **ihtiyaç anında** üretilir.
- **Tablo Soyutlaması:** `DDContainer` → `DDTable` → `DDRow` → `IDBox` silsilesiyle verinizi **StrongType** (sıkı şema) veya **Free** (esnek) davranışıyla modelleyin.
- **Null ve Boxing Kontrolü:** Value-type’larda `GetVal`/`GetString` çağrılarında kaçınılmaz boxing dışında hot-path’te ekstra maliyet yoktur.
- **Minimal Bağımlılık, Maksimum Taşınabilirlik:** Sade .NET API’leri üzerinde kuruludur; herhangi bir ORM veya framework’e kilitli değildir.

>
> **Not:** Eşzamanlı erişim için ayrı sarmalayıcı gerekebilir. Varsayılan sınıflar thread-safe değildir (tek iş parçacığı veya dış senkronizasyon varsayılır).

---

## 📦 Kurulum
Henüz bir NuGet paketi yayımlamıyorsanız iki hızlı seçenek:
1) **Kaynak olarak ekleme:** `DSO.Core.MagicBox` projesini solution’a dahil edin ve referans verin.
2) **Klasör kopyalama:** İlgili dosyaları projenize dahil edin.

> NuGet’e yayımlandığında: `dotnet add package DSO.Core.MagicBox` (README güncellenecektir.)

---

## 🚀 Hızlı Başlangıç
### MagicBox ile değer saklama & okuma
```csharp
using DSO.Core.MagicBox;

var box = new DMagicBox();

// Hücre ekleme (değer capture eden closure ile):
int i = box.Add(42);               // Func<int>
int s = box.Add("Merhaba");       // Func<string>
int n = box.Add(DateTime.UtcNow);  // Func<DateTime>

// Sağlayıcı ile ekleme (değere erişildiğinde hesaplanır):
int r = box.AddFunc(() => Guid.NewGuid().ToString("N")); // Func<string>

// Tür-güvenli çekim
int fortyTwo = box.Get<int>(i);          // 42
string hello = box.Get<string>(s);       // "Merhaba"
DateTime ts = box.Get<DateTime>(n);

// Tip uyumsuzluğu: TryGet ile güvenli kontrol
if (box.TryGet<double>(i, out var dbl) == false)
{
    // listedeki delegenin tipi double değil
}

// Tip belirtmeden object/string alma (DynamicInvoke YOK)
object any = box.GetVal(r);
string text = box.GetString(r);

// Hücre güncelleme
box.Set(i, 100);                         // mevcut hücreyi yeni değerle değiştir
box.SetFunc(s, () => "Daha sonra!");     // sağlayıcı ile değiştir

// Silme & Temizleme
box.RemoveAt(n);
box.Clear();
```

### IDBox/Handle üzerinden çalışma
`DMagicBox` enumerator’ı `IDBox` döndürür; hücreye doğrudan işlemler yapabilirsiniz:
```csharp
foreach (IDBox cell in box)
{
    string sVal = cell.Get<string>();
    object oVal = cell.GetVal();
    string asText = cell.GetString();
}
```

---

## 🧱 Tablo Modeli: DDContainer → DDTable → DDRow → IDBox
`DDContainer` birden fazla tabloyu taşır; her `DDTable` kolon koleksiyonu ve satır koleksiyonuna sahiptir.

### Kolon Davranışı
- `ColumnBehaviour.StrongType` → Kolon tipleri şema olarak korunur; uyumsuz tip set etmeye çalışırsanız açık ve anlaşılır bir hata alırsınız.
- `ColumnBehaviour.Free` → Kolonlar esnektir; indeks sıralamasına göre doldurabilirsiniz.

### Basit Örnek (StrongType)
```csharp
using DSO.Core.MagicBox;

var db = new DDContainer();
var table = db.AddTable("People", ColumnBehaviour.StrongType);

// Şema: Kolonları önceden tanımlayın
var cols = table.ColumnNames;
cols.AddColumn("Id", typeof(int));
cols.AddColumn("Name", typeof(string));
cols.AddColumn("Birth", typeof(DateTime));

// Satır ekleme
var row = table.NewRow();
row.Set(0, 1);                                 // Id
row.Set("Name", "Ada");                       // Name (kolon ismine göre)
row.SetFunc("Birth", () => DateTime.UtcNow);   // Birth sağlayıcı ile

table.Rows.AddRow(row);

// Okuma
int id = row.Get<int>(0);
string name = row.Get<string>("Name");
DateTime birth = row.Get<DateTime>(2);
string birthText = row.GetString(2);
```

### Esnek Örnek (Free)
```csharp
var db = new DDContainer();
var table = db.AddTable("Metrics", ColumnBehaviour.Free);

var r1 = table.NewRow();
r1.Set(123);                 // index 0
r1.Set("req/sec");          // index 1
r1.SetFunc(() => 99.9);      // index 2

table.Rows.AddRow(r1);
```

> `StrongType` modunda şema kolon sayısı ve tipleri ile korunur. `Free` modunda indeks/ekleme sırası önemlidir; isterseniz sonradan kolon isimleri ekleyebilirsiniz.

---

## 🧩 API Kısa Rehberi
### Çekirdek Tipler
- **`IDMagicBox` / `DMagicBox`**: Hücre listesi (delege tabanlı).
- **`IDBox` / `DBox`**: Tek hücre handle’ı; `Get<T>`, `TryGetVal`, `GetString`, `Set*` API’leri.
- **`DDContainer`**: Birden çok tabloyu barındırır.
- **`DDTable`**: Kolon/Şema (`DDColumnCollection`) ve Satırlar (`DDRowCollection`).
- **`DDRow`**: Hücre operasyonları; indeks veya kolon adına göre `Set`, `SetFunc`, `SetDelegate` ve `Get`/`TryGet`/`GetVal`/`GetString`.
- **`DDBox`**: Tek boyutlu kutu (sadece `DMagicBox` + isim).

### Önemli Yöntemler (seçilmiş)

#### `DMagicBox`
- `Add<T>(T value)` / `AddFunc<T>(Func<T>)` / `AddDelegate(Delegate)`
- `Get<T>(int index)` / `TryGet<T>(int index, out T)`
- `GetVal(int index)` / `TryGetVal(int index, out object)`
- `GetString(int index)`
- `Set<T>(int index, T value)` / `SetFunc<T>(int index, Func<T>)` / `SetDelegate(int index, Delegate)`
- `RemoveAt(int index)` / `Remove(IDBox)` / `Clear()`
- `GetEnumerator()` → `IDBox` döndürür

#### `IDBox` (`DBox`)
- `Get<T>()` / `TryGet<T>(out T)`
- `GetVal()` / `TryGetVal(out object)` / `GetString()`
- `Set<T>(T)` / `SetFunc<T>(Func<T>)` / `SetDelegate(Delegate)`
- `IsType<T>()` / `PeekType()`

#### `DD*` Ailesi
- `DDContainer.TryAddTable(string, ColumnBehaviour, out DDTable)` / `AddTable(...)` / `RemoveTable(...)`
- `DDTable.ColumnNames` (`DDColumnCollection`) → `AddColumn(name, type)`, `RemoveColumn`, indexer, sorgular
- `DDTable.Rows` (`DDRowCollection`) → `AddRow(DDRow)` / `AddRow<T>(T entity)` / `AddRowRange<T>(IEnumerable<T>)`
- `DDRow` → `Set/SetFunc/SetDelegate` (indeks, isim), `Get/ TryGet/ GetVal/ GetString`, `Remove/RemoveAt`, `GetEnumerator()`

>
> **Not:** `DDRowCollection.AddRow<T>(T entity)` içerisinde bir POCO nesnesini tabloya aktarma işlemi için dahili yardımcılar (örn. `ToDelegateArray(...)`) kullanılır. Projenize dahil ettiğiniz sürümde bu yardımcıların mevcut olduğundan emin olun. Aksi halde `DDRow` API’leri ile manuel set edebilirsiniz.

---

## ⚙️ İç Mimari ve Performans
- **DynamicInvoke yok:** Tüm okumalarda *tip özelinde* derlenmiş invoker’lar kullanılır (`InvokerCache`).
- **Invoker Cache:** `Func<T>` tipine göre bir defa Expression ile `Func<Delegate, object>` ya da `Func<Delegate, string>` üretilir ve cache’lenir.
- **Boxing:** `Get<T>()` ve `TryGet<T>(...)` **boxing yapmaz**. `GetVal`/`GetString` value-type’larda doğal olarak boxing gerektirir.
- **Null Semantiği:** `GetString()` referans tipler için `null` değeri görürse `""` (boş) döndürür; value-type’lar için ToString kullanır.
- **Thread-Safety:** Varsayılan yapı *thread-safe değildir*. Çok-iş parçacıklı senaryolar için dış senkronizasyon ya da eşzamanlı koleksiyon sarmalayıcıları kullanın.

---

## 🧭 Sınırlamalar & Davranışlar
- **Yalnızca `Func<T>` (0 parametre) delege hücreleri** desteklenir. (API bunu garantiler.)
- **Tip Uyumsuzluğu:** `StrongType` modunda beklenen tip ile farklı bir tip set etmeye çalışırsanız, açıklayıcı bir mesajla hata alırsınız.
- **Dizin Sınırları:** Negatif ya da aralık dışı indeksler güvenle reddedilir.

---

## ❓ SSS
**Neden değerleri doğrudan saklamak yerine `Func<T>` ile saklıyoruz?**
- Laziness (erişimde üretim), test edilebilirlik (mock sağlayıcılar), sabit hot-path, tip güvenliği ve DynamicInvoke kaçınma avantajları için.

**`GetString()` neden özel bir cache kullanıyor?**
- `string` dönüşleri çok yaygın olduğu için, `Func<T>` → `string` yolunu Expression ile derleyip cache’leyerek hot-path’i hızlandırır.

**`DDRowCollection.AddRow<T>(entity)` nasıl kolonları dolduruyor?**
- İlk satırda kolon isimleri/tipleri çıkarılıp tabloya eklenebilir (StrongType). Sonraki satırlarda aynı sırayla set edilir. Projede yer alan yardımcılar (ör. `ToDelegateArray`) üzerinden çalışır.

---

## 🧪 Örnek: Basit Rapor Tablosu
```csharp
var db = new DDContainer();
var t = db.AddTable("Report", ColumnBehaviour.StrongType);

// Şema
var cols = t.ColumnNames;
cols.AddColumn("Key", typeof(string));
cols.AddColumn("Value", typeof(double));
cols.AddColumn("When", typeof(DateTime));

// Satırlar
var r1 = t.NewRow();
r1.Set("Key", "CPU");
r1.Set("Value", 0.73);
r1.SetFunc("When", () => DateTime.UtcNow);
t.Rows.AddRow(r1);

var r2 = t.NewRow();
r2.Set("Key", "Mem");
r2.Set("Value", 0.42);
r2.Set(DateTime.UtcNow);
t.Rows.AddRow(r2);

// Okuma
foreach (var row in t.Rows)
{
    Console.WriteLine($"{row.Get<string>(0)} = {row.Get<double>(1)} @ {row.GetString(2)}");
}
```

---

## 🛠️ Geliştirme, Test ve Katkıda Bulunma
1. Repo’yu klonlayın, çözümü açın.
2. Test/örnek projeleri çalıştırın.
3. Değişiklikler için PR açın. Lütfen:
   - Kod stili ve mimariye uygunluk,
   - Birim testleri (varsa),
   - README/örneklerin güncellenmesi konularına dikkat edin.

> Issue’lar, fikirler ve kullanım örnekleri memnuniyetle karşılanır. Bu çekirdeği birlikte daha güçlü hâle getirelim.

---

## 📅 Yol Haritası (Öneri)
- [ ] Thread-safe varyantlar ve/veya lock-free denemeler
- [ ] Genişletilmiş dönüştürücüler (POCO ↔️ tablo)
- [ ] NuGet paketi ve sürüm stratejisi (SemVer)
- [ ] Kaynak üreteci (Source Generator) ile otomatik kolon/row bağlama

---

## 📄 Lisans
Tavsiye edilen: **MIT**. (Kurumsal gereksinimlerinize göre değiştirebilirsiniz.)

---

## 🙌 Teşekkür
Bu çekirdek, gerçek üretim senaryolarında sınanmış bir şirket içi framework’ten doğdu. Açık kaynak topluluğuna sunarak, birlikte daha hızlı, daha yalın ve daha güvenilir çözümler üretmeyi hedefliyoruz.

