# Jellyfin Subtitle Translator

A UI-driven subtitle translation system with React + Tailwind frontend and C# backend API.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   React UI      │────▶│  .NET API       │────▶│  LibreTranslate │
│  (Port 3000)    │     │  (Port 5001)    │     │  (Port 5000)    │
└─────────────────┘     └────────┬────────┘     └─────────────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │    Jellyfin     │
                        │   (Port 8096)   │
                        └─────────────────┘
```

## Features

- **Web UI**: Browse and select media items from Jellyfin libraries
- **Manual Translation**: Translate subtitles on-demand with one click
- **Batch Translation**: Select multiple items and translate together
- **Real-time Status**: See translation progress for each item
- **Filter & Search**: Filter by media type (movies/episodes) and library
- **Jellyfin Integration**: Fetches media directly from Jellyfin API

## Quick Start

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- Docker (for containerized deployment)
- Jellyfin server with API key

### Local Development

1. Start LibreTranslate:
```bash
docker run -d -p 5000:5000 libretranslate/libretranslate
```

2. Start the backend:
```bash
cd src/JellyfinSubtitleTranslator
dotnet run
```

3. Start the frontend:
```bash
cd frontend
npm install
npm run dev
```

4. Open http://localhost:3000

### Docker Deployment

```bash
# Set Jellyfin credentials
export JELLYFIN_API_KEY=your_api_key
export JELLYFIN_USER_ID=your_user_id

# Start all services
docker-compose up -d
```

Access the UI at http://localhost:3000

## Configuration

### Backend (appsettings.json)

```json
{
  "Translator": {
    "MediaPath": "/media",
    "TargetLanguage": "rus",
    "LibreTranslateUrl": "http://localhost:5000",
    "MaxBatchSize": 50,
    "MaxConcurrency": 2,
    "Jellyfin": {
      "BaseUrl": "http://localhost:8096",
      "ApiKey": "9653fc3129a0497f9e5c3679351b9e40",
      "UserId": "mike"
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5001"
      }
    }
  }
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `JELLYFIN_API_KEY` | Jellyfin API key |
| `JELLYFIN_USER_ID` | Jellyfin user ID |
| `Translator__TargetLanguage` | Target language (default: rus) |
| `Translator__LibreTranslateUrl` | LibreTranslate API URL |

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/media` | List media items |
| GET | `/api/media/libraries` | List Jellyfin libraries |
| POST | `/api/translate/manual` | Translate single item |
| POST | `/api/translate/batch` | Batch translate |
| GET | `/api/translate/discover` | Discover subtitles |

## Project Structure

```
├── src/JellyfinSubtitleTranslator/
│   ├── Configuration/
│   │   └── TranslatorOptions.cs
│   ├── Controllers/
│   │   ├── MediaController.cs
│   │   └── TranslateController.cs
│   ├── Models/
│   ├── Services/
│   │   ├── Jellyfin/
│   │   │   └── JellyfinService.cs
│   │   ├── SubtitleTranslationService.cs
│   │   └── ...
│   ├── Program.cs
│   └── appsettings.json
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── services/
│   │   ├── types/
│   │   └── App.tsx
│   └── Dockerfile
├── docker-compose.yml
└── README.md
```

## Jellyfin Setup

1. Enable API access in Jellyfin:
   - Dashboard → Server → Advanced → API Key
   - Create a new API key

2. Find your User ID:
   - Go to your user settings
   - Copy the user ID from the URL or settings page

3. Ensure media folders match between Jellyfin and this service

## Supported Languages

- Russian (`rus`)
- Hebrew (`heb`)
- English (`eng`)
- Arabic (`ara`)
- And all languages supported by LibreTranslate

## License

MIT
