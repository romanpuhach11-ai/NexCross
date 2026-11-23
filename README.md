# NexCross

Цей репозиторій містить вихідний код програми **Crosshair Studio** у папці [`CrosshairStudio`](CrosshairStudio).

## Що вміє версія 1.0

- головний Launcher з банером та навігацією;
- окремі розділи Studio, Presets і Settings;
- показує налаштовуваний приціл як прозорий оверлей поверх вікон;
- підтримує форми Dot, Circle, Square, Cross і T;
- налаштовує розмір, товщину, обводку, прозорість, зсув X/Y і два кольори;
- підтримує центральну точку, пульсацію та анімацію віддачі;
- завантажує зображення приціла та копіює їх у локальну папку застосунку;
- зберігає пресети у JSON і дозволяє вибір монітора;
- працює в треї та перевіряє нові релізи через GitHub Releases;
- завантажує portable ZIP, перевіряє SHA-256 із `checksums.txt` і після підтвердження запускає оновлення.

Програма не читає пам’ять ігри й не змінює ігрові процеси чи файли. Підтримані режими оверлея залежать від гри, системи захоплення та режиму дисплея.

## Вимоги

- Windows 10/11;
- .NET 8 SDK для запуску із вихідного коду;
- доступ до Інтернету для перевірки GitHub Releases.

## Запуск із вихідного коду

```powershell
cd CrosshairStudio
dotnet restore
dotnet run --project CrosshairStudio.csproj
```

## Збірка релізу

```powershell
cd CrosshairStudio
Set-ExecutionPolicy -Scope Process Bypass
.\build-release.ps1
```

Скрипт створює самоодержний `.zip` у папці `CrosshairStudio\release` і контрольні суми в `release\checksums.txt`.

GitHub Actions у `.github/workflows/release.yml` автоматично збирає ZIP і `checksums.txt` і додає їх до опублікованого GitHub Release.

## Установлення

```powershell
cd CrosshairStudio
Set-ExecutionPolicy -Scope Process Bypass
.\install.ps1
```

Скрипт встановлює програму в `%LocalAppData%\CrosshairStudio`, створює ярлик у меню «Пуск» і реєструє запис видалення Windows.

## Видалення

```powershell
cd CrosshairStudio
Set-ExecutionPolicy -Scope Process Bypass
.\uninstall.ps1
```

Скрипт видаляє встановлені файли, ярлик і запис видалення. Пресети і зображення окремо зберігаються у `%LocalAppData%\CrosshairStudio\Presets` і `%LocalAppData%\CrosshairStudio\Images`.

## Гарячі клавіші

- `Ctrl+Alt+X` — показати або приховати оверлей;
- `Ctrl+Alt+Z` — перейти до наступного збереженого пресета.

Хрестик головного вікна приховує програму в трей. Вихід доступний через контекстне меню значка в треї.

## Збереження даних

- пресети: `%LocalAppData%\CrosshairStudio\Presets`;
- зображення: `%LocalAppData%\CrosshairStudio\Images`;
- загальний каталог застосунку: `%LocalAppData%\CrosshairStudio`.

## Обмеження оверлея

- у режимі exclusive fullscreen оверлей зазвичай не відображається;
- повноекранні DirectX/OpenGL застосунки можуть блокувати сторонні вікна;
- програма може не відображатися над вікнами з вищими правами;
- захоплення екрана, трансляція чи інші інструменти можуть приховувати сторонній оверлей;
- положення й масштаб залежать від вибраного монітора й DPI Windows.

## Античіти та правила ігрових платформ

Не намагайтеся обходити захист античитів або правила сервера. Перед використанням перевірте, чи дозволений сторонній оверлей конкретною грою чи платформою. Античіт може блокувати або банити акаунт незалежно від того, чи не читає програма пам’ять ігри чи не змінює її файли.