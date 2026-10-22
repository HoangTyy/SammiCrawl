# SammiCrawl

Hệ thống SammiCrawl bao gồm 2 phần chính:

- **Frontend**: Ứng dụng web React + TypeScript xây dựng với Vite.
- **Backend**: RESTful Web API xây dựng với ASP.NET Core (.NET 10.0), Controller-based, đã tích hợp OpenAPI và CORS sẵn cho Frontend.

---

## 1. Frontend (React + TypeScript + Vite)

### Cài đặt dependencies
```powershell
cd frontend
npm install
```

### Chạy môi trường Development
```powershell
npm run dev
```
Mặc định ứng dụng chạy tại: `http://localhost:5173`

### Build Production
```powershell
npm run build
```

---

## 2. Backend (ASP.NET Core Web API)

### Build dự án
```powershell
cd backend
dotnet build
```

### Chạy dự án
```powershell
dotnet run
```
Mặc định API sẽ khởi động và cung cấp endpoint:
- **WeatherForecast sample**: `GET /weatherforecast`
- **OpenAPI / Swagger spec**: `GET /openapi/v1.json`
