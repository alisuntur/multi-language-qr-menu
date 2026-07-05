# Multi Language QR Menu

Sprint 0 ve Sprint 1 iskeleti bu repoda yer alır.

## İçerik

- ASP.NET Core Web API backend
- PostgreSQL bootstrap scripti
- JWT + refresh token auth akışı
- React + Vite + TypeScript admin/public iskeleti
- Restoran ayarları ve rol bazlı panel görünürlüğü

## Varsayılan Giriş Bilgileri

- E-posta: `owner@demoqrmenu.local`
- Şifre: `ChangeMe123!`

## Çalıştırma

### Backend

```powershell
dotnet restore backend/MultiLanguageQrMenu.Api/MultiLanguageQrMenu.Api.csproj
dotnet run --project backend/MultiLanguageQrMenu.Api
```

### Frontend

```powershell
cd frontend
npm install
npm run dev
```
