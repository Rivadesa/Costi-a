import { HttpTransportType, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { call, type Fetch } from './api'
import type { HubLike } from './live'

// Un navegador no puede enviar cabeceras en un WebSocket. Por eso existe el BILLETE EFIMERO del hub
// (D4.3a): un solo uso, 60 s, valido solo en la ruta del hub. Se pide uno NUEVO en cada conexion y en
// cada reconexion (accessTokenFactory); el token del dispositivo jamas viaja en una URL. Sin
// negociacion previa: una sola peticion WebSocket por conexion = un solo billete gastado.
export function connectHub(fetcher: Fetch, deviceToken: string): HubLike {
  const connection = new HubConnectionBuilder()
    .withUrl('/api/native/v1/events', {
      skipNegotiation: true,
      transport: HttpTransportType.WebSockets,
      accessTokenFactory: async () => (await call<{ hubToken: string }>(fetcher, '/auth/hub-token', { token: deviceToken, body: {} })).hubToken,
    })
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10_000, 10_000])
    .configureLogging(LogLevel.None)      // nada de URLs con billete en la consola
    .build()
  return {
    start: () => connection.start(),
    stop: () => connection.stop(),
    onEvent: handler => connection.on('event', handler),                  // el contenido del aviso se ignora a proposito
    onReconnecting: handler => connection.onreconnecting(handler),
    onReconnected: handler => connection.onreconnected(handler),
    onClosed: handler => connection.onclose(handler),
  }
}
