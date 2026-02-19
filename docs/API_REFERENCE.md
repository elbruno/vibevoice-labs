# API Reference

This document provides complete documentation for the VibeVoice Labs REST API.

## Base URL

| Environment | URL |
|-------------|-----|
| Development | `http://localhost:5100` |
| Via Aspire | `http://backend` (service discovery) |

## Authentication

The API currently requires no authentication. In production, consider adding:
- API key authentication
- JWT tokens
- Rate limiting

---

## Endpoints

### Health Check

Check the service status and model availability.

```http
GET /api/health
```

#### Response

```json
{
  "status": "healthy",
  "model_loaded": true
}
```

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | `"healthy"` or `"unhealthy"` |
| `model_loaded` | boolean | Whether the TTS model is loaded |

#### Status Codes

| Code | Description |
|------|-------------|
| `200` | Service is running |

#### Example

```bash
curl http://localhost:5100/api/health
```

---

### List Voices

Get all available TTS voices with metadata.

```http
GET /api/voices
```

#### Response

```json
{
  "voices": [
    {
      "id": "en-carter",
      "name": "Carter",
      "language": "en",
      "style": "male"
    },
    {
      "id": "en-emma",
      "name": "Emma",
      "language": "en",
      "style": "female"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `voices` | array | List of voice objects |
| `voices[].id` | string | Unique voice identifier (use in TTS requests) |
| `voices[].name` | string | Human-readable voice name |
| `voices[].language` | string | Language code (BCP-47 format) |
| `voices[].style` | string | Voice style (e.g., `"general"`, `"conversational"`) |

#### Status Codes

| Code | Description |
|------|-------------|
| `200` | Success |

#### Example

```bash
curl http://localhost:5100/api/voices
```

---

### Generate Speech

Convert text to speech using the specified voice.

```http
POST /api/tts
Content-Type: application/json
```

#### Request Body

```json
{
  "text": "Hello, world!",
  "voice_id": "en-carter",
  "output_format": "wav"
}
```

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `text` | string | Yes | - | Text to synthesize (1-1000 characters) |
| `voice_id` | string | No | `"en-carter"` | Voice ID from `/api/voices` |
| `output_format` | string | No | `"wav"` | Audio format (only `"wav"` supported) |

#### Response

**Success (200):**
- `Content-Type: audio/wav`
- `Content-Disposition: attachment; filename=speech.wav`
- Body: Binary WAV audio data

**Error (4xx/5xx):**
```json
{
  "error": "Text is required",
  "code": "VALIDATION_ERROR"
}
```

#### Status Codes

| Code | Description |
|------|-------------|
| `200` | Success - returns WAV audio |
| `400` | Validation error (invalid input) |
| `500` | Server error (model failure) |

#### Error Codes

| Code | Description |
|------|-------------|
| `VALIDATION_ERROR` | Missing or invalid request parameters |
| `UNSUPPORTED_FORMAT` | Requested format not supported |
| `MODEL_ERROR` | TTS model not loaded or unavailable |
| `GENERATION_ERROR` | Audio generation failed |

#### Examples

**Basic Request:**
```bash
curl -X POST http://localhost:5100/api/tts \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello, world!"}' \
  --output speech.wav
```

**With Voice Selection:**
```bash
curl -X POST http://localhost:5100/api/tts \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Guten Tag! Willkommen bei VibeVoice.",
    "voice_id": "en-emma"
  }' \
  --output german_speech.wav
```

**Python Example:**
```python
import requests

response = requests.post(
    "http://localhost:5100/api/tts",
    json={
        "text": "Hello from Python!",
        "voice_id": "en-carter"
    }
)

if response.status_code == 200:
    with open("output.wav", "wb") as f:
        f.write(response.content)
else:
    print(f"Error: {response.json()}")
```

**JavaScript Example:**
```javascript
const response = await fetch('http://localhost:5100/api/tts', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    text: 'Hello from JavaScript!',
    voice_id: 'en-carter'
  })
});

if (response.ok) {
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const audio = new Audio(url);
  audio.play();
}
```

---

## Voice Reference

### Available Voices

VibeVoice uses pre-computed voice preset files (.pt) for each speaker.

| ID | Name | Gender | Preset File |
|----|------|--------|-------------|
| `en-carter` | Carter | Male | `en-Carter_man.pt` |
| `en-davis` | Davis | Male | `en-Davis_man.pt` |
| `en-emma` | Emma | Female | `en-Emma_woman.pt` |
| `en-frank` | Frank | Male | `en-Frank_man.pt` |
| `en-grace` | Grace | Female | `en-Grace_woman.pt` |
| `en-mike` | Mike | Male | `en-Mike_man.pt` |

Voice presets are automatically downloaded from the [VibeVoice GitHub repo](https://github.com/microsoft/VibeVoice/tree/main/demo/voices/streaming_model) on first use.

---

## Audio Specifications

| Property | Value |
|----------|-------|
| Format | WAV (RIFF) |
| Sample Rate | 24000 Hz |
| Channels | Mono |
| Bit Depth | 16-bit |
| Encoding | PCM |

---

## Rate Limits

Currently no rate limiting is implemented. For production:

| Limit | Recommendation |
|-------|----------------|
| Requests/minute | 60 |
| Max text length | 1000 characters |
| Max concurrent | 10 requests |

---

## OpenAPI Documentation

Interactive API documentation is available at:

- **Swagger UI:** `http://localhost:5100/docs`
- **ReDoc:** `http://localhost:5100/redoc`
- **OpenAPI JSON:** `http://localhost:5100/openapi.json`

---

## Error Handling

### Error Response Format

All errors return JSON with this structure:

```json
{
  "error": "Human-readable error message",
  "code": "MACHINE_READABLE_CODE"
}
```

### Common Error Scenarios

**Empty Text:**
```json
{
  "error": "Text is required",
  "code": "VALIDATION_ERROR"
}
```

**Text Too Long:**
```json
{
  "error": "ensure this value has at most 1000 characters",
  "code": "VALIDATION_ERROR"
}
```

**Unsupported Format:**
```json
{
  "error": "Only 'wav' format is supported",
  "code": "UNSUPPORTED_FORMAT"
}
```

**Model Not Loaded:**
```json
{
  "error": "TTS model not available",
  "code": "MODEL_ERROR"
}
```

---

## Best Practices

### Text Input

1. **Use proper punctuation** — Improves prosody and pacing
2. **Keep sentences reasonable** — 10-30 words per sentence
3. **Avoid special characters** — May cause unexpected pauses
4. **Test with sample texts** — Verify voice quality before production

### Voice Selection

1. **Match language to text** — Use German voice for German text
2. **Consider audience** — Use appropriate accent/region
3. **Test multiple voices** — Find the best fit for your use case

### Performance

1. **Cache generated audio** — Avoid re-generating same text
2. **Use connection pooling** — Reuse HTTP connections
3. **Handle errors gracefully** — Implement retry logic
4. **Monitor model health** — Check `/api/health` periodically
