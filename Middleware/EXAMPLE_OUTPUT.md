# Example Output from NetworkMonitor

This document shows example output from the modified NetworkMonitor middleware based on the reference from the problem statement.

## HTTP Request/Response Capture Example

Based on the reference output format:
```
[2025-11-24 12:03:41] HTTP Request (POST /books HTTP/1.1) ::1:52387 -> ::1:5000
POST /books HTTP/1.1
Host: localhost:5000
Content-Type: application/json; charset=utf-8
Content-Length: 51

{"BookId":0,"Title":"Book1","PublicationYear":2025}
```

The NetworkMonitor now outputs:

```json
[2025-11-24 12:03:41] HTTP REQUEST Capture:
{
  "Info": "HTTP Request (POST /books HTTP/1.1)",
  "Source": "::1:52387 (Client)",
  "Destination": "::1:8888 (Proxy -> Server:5000)",
  "Flags": "POST",
  "State": "REQUEST",
  "URI": "/books",
  "Host": "localhost:5000",
  "Method": "POST",
  "Status": null,
  "HttpVersion": "HTTP/1.1",
  "HttpHeaders": "Host: localhost:5000\nContent-Type: application/json; charset=utf-8\nContent-Length: 51",
  "HttpBody": "{\"BookId\":0,\"Title\":\"Book1\",\"PublicationYear\":2025}"
}
--------------------------------------------------------------------------------
```

And for the response:

```json
[2025-11-24 12:03:41] HTTP RESPONSE Capture:
{
  "Info": "HTTP Response (HTTP/1.1 201 Created)",
  "Source": "::1:8888 (Proxy <- Server:5000)",
  "Destination": "::1:52387 (Client)",
  "Flags": "201 Created",
  "State": "RESPONSE",
  "URI": "/books",
  "Host": "localhost:5000",
  "Method": "POST",
  "Status": "201 Created",
  "HttpVersion": "HTTP/1.1",
  "HttpHeaders": "Content-Length: 67\nContent-Type: application/json\nServer: Microsoft-HTTPAPI/2.0\nDate: Mon, 24 Nov 2025 05:03:40 GMT",
  "HttpBody": "{\"BookId\":11,\"Title\":\"Book1\",\"PublicationYear\":2025,\"GenreId\":null}"
}
--------------------------------------------------------------------------------
```

## Key Improvements

1. **Structured JSON Output**: All data is now in a consistent, parseable JSON format
2. **Complete Field Set**: Every capture includes all required fields (HTTP or TCP specific)
3. **Client/Server Labeling**: Endpoints are clearly labeled based on port matching
4. **Null-Safe**: Fields that don't apply are properly set to null
5. **Timestamp**: Each capture includes a timestamp for tracking
6. **Separation**: Clear visual separation between captures with horizontal lines

## Field Mapping

### TCP Captures
| Excel Field | JSON Field | Example Value |
|-------------|------------|---------------|
| Info | Info | "TCP Data (Client -> Server)" |
| Source | Source | "::1:52387 (Client)" |
| Destination | Destination | "::1:5000 (Server)" |
| Flags | Flags | "PSH, ACK" |
| State | State | "REQUEST" or "RESPONSE" |
| Data | Data | "TCP payload content" |

### HTTP Captures
| Excel Field | JSON Field | Example Value |
|-------------|------------|---------------|
| Info | Info | "HTTP Request (POST /books HTTP/1.1)" |
| Source | Source | "::1:52387 (Client)" |
| Destination | Destination | "::1:8888 (Proxy -> Server:5000)" |
| Flags | Flags | "POST" or "201 Created" |
| State | State | "REQUEST" or "RESPONSE" |
| URI | URI | "/books" |
| Host | Host | "localhost:5000" |
| Method | Method | "POST" |
| Status | Status | "201 Created" (null for requests) |
| HTTP Version | HttpVersion | "HTTP/1.1" |
| Http Headers | HttpHeaders | "Host: localhost:5000\nContent-Type: ..." |
| Http Body | HttpBody | "{\"BookId\":11,...}" |
