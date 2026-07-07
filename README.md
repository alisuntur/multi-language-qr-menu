# Multi Language QR Menu

Sprint 0-5 kapsami bu repoda yer alir.

## Icerik

- ASP.NET Core Web API backend
- PostgreSQL bootstrap scripti
- JWT + refresh token auth akisi
- React + Vite + TypeScript admin paneli
- Public QR menu, dil secici ve restoran bazli aktif dil yonetimi
- Kategori, urun ve restoran ayarlari yonetimi

## Varsayilan Giris Bilgileri

- E-posta: `owner@demoqrmenu.local`
- Sifre: `ChangeMe123!`

## Calistirma

### Backend

Ilk kurulum:

```powershell
dotnet restore backend/MultiLanguageQrMenu.Api/MultiLanguageQrMenu.Api.csproj
dotnet build backend/MultiLanguageQrMenu.Api/MultiLanguageQrMenu.Api.csproj
```

Normal calistirma:

```powershell
dotnet run --project backend/MultiLanguageQrMenu.Api --urls http://127.0.0.1:5099
```

Eger `MultiLanguageQrMenu.Api.dll is being used by another process` hatasi alirsan, eski backend surecini kapatip yeniden calistir:

```powershell
Get-Process dotnet
Stop-Process -Id <PID> -Force
dotnet run --project backend/MultiLanguageQrMenu.Api --urls http://127.0.0.1:5099
```

Saglik kontrolu:

```powershell
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5099/health | Select-Object -ExpandProperty Content
```

### Frontend

```powershell
cd frontend
npm install
npm run dev
```

### Public Menu

- Admin panel: `http://127.0.0.1:5173/admin`
- Public menu: `http://127.0.0.1:5173/menu/demo-qr-bistro`
