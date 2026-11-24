# Lasec HART Communication DTM

Comm DTM FDT 1.2 (mestre HART) para PACTware e outros Frames FDT 1.x. Faz o mesmo papel do "HART Communication" da CodeWrights: oferece um canal HART ao qual os DTMs de instrumento se conectam. Também funciona sem fio.

| Meio | Uso |
|---|---|
| Modem HART em porta COM | RS-232, USB ou Bluetooth (perfil SPP) |
| HART-IP UDP/TCP (porta 5094) | Gateways WirelessHART, multiplexadores e instrumentos HART-IP |
| Ponte UDP | Quadro HART bruto num datagrama (para pontes Wi-Fi próprias) |

## Estrutura

| Caminho | Conteúdo |
|---|---|
| `src/HartEngine` | Mestre HART, independente de FDT. Contém:<br>• quadros, parser (tolera lixo, eco, burst e fragmentos) e checksum<br>• transportes serial (Win32, com RTS controlado), HART-IP e ponte UDP<br>• repetições e resposta atrasada (códigos 32/33/34)<br>• varredura (comandos 0, 13, 74 e 84)<br>• log de comunicação |
| `src/LasecHartCommDTM` | O DTM, sobre o interop oficial `Jigfdt.fdt100`:<br>• `CommDtm`: `IDtm`, `IDtm2`, `IDtmInformation`, `IDtmChannel`, `IDtmParameter`, `IDtmActiveXInformation`, `IFdtEvents`, `IPersistPropertyBag`, `IPersistStreamInit`<br>• `HartChannel`: `IFdtChannel`, `IFdtCommunication`, `IFdtChannelSubTopology`<br>• `ConfigurationControl`: a tela ActiveX |
| `tests/HartEngine.Tests` | Testes de unidade do mestre HART (xUnit) |
| `installer/Product.wxs` | MSI (WiX 3), gerado pelo workflow |
| `dlls/` | `FDT100.dll` e `JIGFDT.FDT100.DLL`, oficiais do FDT-JIG. `FDTInterop.dll`, o tlbimp antigo, não é mais usado |

## Compilar, testar e registrar

```powershell
dotnet build LasecHartCommDTM.sln -c Release
dotnet test tests\HartEngine.Tests
# Registro COM (prompt como administrador). Os Frames FDT 1.x são de 32 bits, então use o RegAsm de 32 bits:
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /codebase src\LasecHartCommDTM\bin\Release\net48\LasecHartCommDTM.dll
```

O registro cria:
- `JosueLab.LasecHartCommDTM.CommDtm` (CLSID `{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}`), nas categorias FDT DTM, Channel, Communication, Presentation ActiveXControl e HART. São as mesmas da CodeWrights.
- O controle `JosueLab.LasecHartCommDTM.ConfigurationControl` (`{…20020}`).

No PACTware, rode "Atualizar catálogo de instrumentos" depois de registrar.

## Funções do DTM

| Função | O que faz |
|---|---|
| Configuração (padrão) | Abas **Conexão** e **Mestre HART**:<br>• conexão: interface, porta, velocidade, paridade, stop bits, RTS, DTR e, para rede, endereço e porta<br>• mestre: primário/secundário, preâmbulos, repetições, tempos e faixa de varredura<br>Inclui "Testar meio", que abre e fecha a porta ou a sessão sem falar com instrumentos |
| Instrumentos na rede | Varredura somente leitura (comandos 0, 13, 74, 84). Lista inclusive os sub-dispositivos de gateways WirelessHART |
| Log de comunicação | Quadros enviados e recebidos, ao vivo. Também ficam em `%TEMP%\LasecHartCommDTM.log` |
| Sobre | Versão e identificação |

A configuração faz parte do data set do DTM e é salva com o projeto do Frame. Ela só pode ser mudada sem instrumentos conectados, como na CodeWrights. Propriedades, todas com nomes simples: `Interface` (`Serial`, `HartIpUdp`, `HartIpTcp`, `UdpBridge`), `Port`, `Baudrate`, `Parity`, `StopBits`, `RtsControl` (`Toggle`, `DriverToggle`, `On`, `Off`), `DtrEnabled`, `RtsDelayMs`, `HostAddress`, `HostPort`, `InactivityTimeoutMs`, `PrimaryMaster`, `PreambleCount`, `RetryCount`, `ResponseTimeoutMs` (0 = automático), `DelayedResponseTimeoutMs`, `ScanStart`, `ScanStop`, `ScanSubDevices`.

## Comportamento no canal (FDT 1.2 §4.5)

- **Conexão** (`ConnectRequest`): o DTM manda o comando 0.
  - Com endereço longo informado, usa o endereço longo e confere o instrumento.
  - Sem ele, usa o endereço de polling.
  - A resposta (`ConnectResponse` ou `CommunicationError noConnection`) chega assíncrona, na thread do DTM.
- **Transações** (`TransactionRequest`): executadas em quadro longo, uma por vez, numa thread de E/S.
  - Erro de comunicação do instrumento ou silêncio: o DTM repete.
  - "Ocupado" e resposta atrasada: o DTM reenvia até concluir.
- **Desconexão**: a resposta vai **antes** de `DisconnectRequest` retornar, como na CodeWrights. Há DTMs de instrumento (ex.: Smar LD301) que dependem disso.
- **Porta**: aberta na primeira conexão e fechada quando o último instrumento desconecta.
- **Varredura de topologia do Frame** (`IFdtChannelSubTopology::ScanRequest`): resposta por `IDtmEvents::OnScanResponse` (DTMTopologyScanSchema), com progresso por `OnProgress`.

## Validação

Feita no LasecFDT (`C:\Source Local\LasecFDT`, doc 08 §11), sempre com instrumentos **simulados**:
- todo XML do DTM é válido contra os schemas XDR do FDT;
- Smar LD301 e TT301 conectam, leem parâmetros e desconectam pelo DTM, via modem serial (par virtual com0com) e via HART-IP UDP/TCP;
- gateway WirelessHART simulado: sub-dispositivos, resposta atrasada e varredura;
- a tela abre e fecha pelo protocolo.

Ainda não testado com equipamento real: modem HART físico e gateway WirelessHART. O procedimento está no doc 10 do LasecFDT.
