# NetworkMonitor Output Format

The NetworkMonitor (Middleware) now outputs structured JSON data for all captured network traffic.

## Output Format

All captures are logged as complete JSON objects with the following structure:

### HTTP Request Capture
```json
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
```

### HTTP Response Capture
```json
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
```

### TCP Request Capture
```json
{
  "Info": "TCP Data (Client -> Server)",
  "Source": "::1:52387 (Client)",
  "Destination": "::1:5000 (Server)",
  "Flags": "PSH, ACK",
  "State": "REQUEST",
  "Data": "TCP payload data here"
}
```

### TCP Response Capture
```json
{
  "Info": "TCP Data (Server -> Client)",
  "Source": "::1:5000 (Server)",
  "Destination": "::1:52387 (Client)",
  "Flags": "PSH, ACK",
  "State": "RESPONSE",
  "Data": "TCP payload data here"
}
```

## Field Descriptions

### Common Fields (Both TCP and HTTP)
- **Info**: Descriptive information about the capture
- **Source**: Source address:port with role label (Client/Server)
- **Destination**: Destination address:port with role label (Client/Server)
- **Flags**: Protocol-specific flags or status
- **State**: Direction/state of the traffic (REQUEST/RESPONSE)

### HTTP-Specific Fields
- **URI**: The request URI path and query string
- **Host**: The Host header value
- **Method**: HTTP method (GET, POST, PUT, DELETE, etc.)
- **Status**: HTTP status code and reason phrase (null for requests)
- **HttpVersion**: HTTP protocol version (e.g., "HTTP/1.1")
- **HttpHeaders**: Formatted string of all HTTP headers
- **HttpBody**: The request or response body/payload

### TCP-Specific Fields
- **Data**: Raw TCP payload data

## Client vs Server Determination

The middleware determines whether an endpoint is a client or server based on the configured server port:
- If the port matches the configured server port → labeled as "Server"
- If the port is different from the server port → labeled as "Client"

## Notes

- All fields are always present in the output object
- Fields that are not applicable may be `null`
- The complete object is logged every time a network flow is captured
- Logs are written with timestamps in the format: `[YYYY-MM-DD HH:mm:ss]`
