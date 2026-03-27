# LasecHartCommDTM — Resumo Completo do Projeto

## O que é
DTM (Device Type Manager) de comunicação HART para PACTware 5.0, escrito em C# .NET Framework 4.8, x86. Substitui o CWHart CommDTM (CodeWrights) usando TCP/IP (UDP/TCP) ao invés de interface serial. A tela de parâmetros mostra IP/Porta/Protocolo ao invés de Serial Interface.

## Referência: CWHart CommDTM (CodeWrights)
- OCX nativo (C++/ATL): `C:\WINDOWS\SysWow64\CWHARTFDT.ocx`
- Templates XML: `C:\Program Files (x86)\Common Files\CodeWrights\CWHARTCommDTM\Common\XmlTemplates\`
- Schemas FDT: `C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\FDT XML Schemas\`
- O CWHart é um controle ActiveX nativo que implementa IOleObject, IOleInPlaceObject, etc. nativamente — PACTware embute ele dentro da janela MDI

---

## Tecnologia & Build

| Item | Valor |
|------|-------|
| Framework | .NET Framework 4.8 (net48) |
| Plataforma | x86 (obrigatório — PACTware é 32-bit) |
| Strong Name | LasecHartCommDTM.snk |
| CLSID CommDtm | `{B4B6B3E7-639D-460B-B9A0-6C7F7EB20010}` |
| CLSID ConfigControl | `{B4B6B3E7-639D-460B-B9A0-6C7F7EB20030}` |
| TypeLib | `{40498B38-0A79-3F68-905F-7954AA76AEA6}` |
| PublicKeyToken | `45a43f6ca7aaed45` |
| Jigfdt.fdt100.dll | `EmbedInteropTypes=true`, `Private=false` |
| PACTware | 5.0 x86, CLR 4.0, usa MSXML4 x-schema XDR validation |

### Comandos de build e deploy
```powershell
# Build
dotnet build src\LasecHartCommDTM\LasecHartCommDTM.csproj -c Release -p:Platform=x86

# Deploy (rodar como ADMINISTRADOR)
cd C:\SourceCode\LasecHartCommDTM
powershell -ExecutionPolicy Bypass -File deploy_admin.ps1
```

O `deploy_admin.ps1`:
1. Copia DLL para `C:\Program Files (x86)\PACTware 5.0\DTM700\JosueLab\LasecHartCommDTM\Dtm\`
2. Desregistra COM antigo
3. Registra com `regasm /codebase /tlb` (CLR 4.0 x86)
4. Cria chaves ActiveX do ConfigControl (Control, MiscStatus, TypeLib, VERSION)
5. Limpa log

### Log de diagnóstico
```
C:\ProgramData\PACTware Consortium e.V\PACTware 5.0\LasecHartDTM.log
```

---

## O QUE JÁ FUNCIONA (NÃO MUDAR!)

### 1. DTM aparece no catálogo do PACTware ✅
O `GetInformation()` retorna XML correto com:
- `<FDTVersion major="1" minor="2" />`
- `busCategory="036D1498-387B-11D4-86E1-00E0987270B9"` (HART)
- `communicationType="supported"`

### 2. Registro COM completo ✅
O `[ComRegisterFunction]` no CommDtm registra 8 categorias:
- FDT DTM (`036D1490`), HART Bus (`036D1498`), FDT 1.2 (`036D1493`)
- FDT CommDTM (`036D1494`), FDT Compat (`036D1495`)
- PersistStreamInit, PersistPropertyBag, Automation Objects
- Subchaves: TypeLib, Programmable, VERSION 1.0, Control

### 3. Ciclo de vida COM funciona ✅
PACTware chama com sucesso (em ordem):
```
IPersistStreamInit.InitNew() → Environment() → InitNew() → SetLanguage() → Config() → PrivateDialogEnabled()
→ IDtmParameter.GetParameters() → IDtmParameter.SetParameters() → GetFunctions()
```

### 4. IDtmParameter funciona ✅
`GetParameters()` e `SetParameters()` leem/escrevem 10 variáveis de configuração:
- ipAddress, ipPort, protocol, primaryMaster, preambleCount, retryCount
- scanStart, scanStop, burstMode, timeout

Formato XML:
```xml
<FDT xmlns="x-schema:DTMParameterSchema.xml" xmlns:fdt="x-schema:FDTDataTypesSchema.xml">
 <DtmDevice tag="...">
  <fdt:DtmVariables>
   <fdt:DtmVariable name="ipAddress"><fdt:Value><fdt:Variant><fdt:StringData string="127.0.0.1"/></fdt:Variant></fdt:Value></fdt:DtmVariable>
   ...
  </fdt:DtmVariables>
 </DtmDevice>
</FDT>
```

### 5. Menu de funções aparece ✅
`GetFunctions()` retorna XML no formato correto (igual ao CWHart):
```xml
<?xml version="1.0"?>
<FDT xmlns="x-schema:DTMFunctionsSchema.xml"
     xmlns:fdt="x-schema:FDTDataTypesSchema.xml"
     xmlns:appId="x-schema:FDTApplicationIdSchema.xml">
 <Functions label="Functions" fdt:name="" help="">
  <StandardFunction fdt:name="Configuration" help="" functionId="1"
   resizableStandardFunction="1" printableStandardFunction="1">
   <Status toggle="0" checked="0" enabled="1" hidden="0" separator="0"/>
   <appId:ApplicationId applicationId="fdtConfiguration"/>
  </StandardFunction>
  <Function label="..." functionId="20" hasGUI="1" resizable="1">
   <Status toggle="1" checked="0" enabled="1" hidden="0" separator="0"/>
  </Function>
  <!-- + functionId 30, 10, 100 -->
 </Functions>
</FDT>
```

### 6. ICustomQueryInterface funciona ✅
Necessário porque `EmbedInteropTypes=true` causa Type Equivalence que pode falhar. O QI manual resolve isso para todas as interfaces FDT.

### 7. IPersistStreamInit e IPersistPropertyBag ✅
Implementados como no-ops. Sem isso o DTM não aparece no catálogo.

---

## Interfaces FDT — GUIDs Confirmados

| Interface | GUID | Status |
|-----------|------|--------|
| IDtmInformation | `{036D147F-387B-11D4-86E1-00E0987270B9}` | ✅ Implementado |
| IDtm | `{036D1481-387B-11D4-86E1-00E0987270B9}` | ✅ Implementado |
| IFdtCommunication | `{039ECFC4-9CA8-44E6-944D-B37F288A34D8}` | ✅ Implementado |
| IDtmParameter | `{036D147D-387B-11D4-86E1-00E0987270B9}` | ✅ Implementado |
| IDtmActiveXInformation | `{036D1480-387B-11D4-86E1-00E0987270B9}` | ✅ Implementado |
| IPersistStreamInit | `{7FD52380-4E07-101B-AE2D-08002B2EC713}` | ✅ Implementado |
| IPersistPropertyBag | `{37D84F60-42CB-11CE-8135-00AA004BB851}` | ✅ Implementado |
| IDtmApplication | `{036D147E-387B-11D4-86E1-00E0987270B9}` | Testado, funciona mas abre diálogo separado |
| IDtm2 | `{51E1F44B-D6A1-423D-B11F-AD38EDE78047}` | Não implementado (PACTware pergunta por QI) |
| IDtmSingleDeviceDataAccess | `{D67240E4-664B-44B0-B692-A1D1ED3FB8F8}` | Não implementado (PACTware pergunta por QI) |
| IDtmChannel | `{036D1489-387B-11D4-86E1-00E0987270B9}` | Não implementado (PACTware pergunta por QI) |

---

## O PROBLEMA ATUAL — ActiveX Embedding Não Funciona

### Comportamento observado
Quando o usuário clica em "parâmetro" no PACTware:

1. PACTware chama `GetFunctions()` → recebe a lista de funções ✅
2. PACTware QI `{036D1480}` (IDtmActiveXInformation) → **HANDLED** ✅
3. PACTware chama `QueryActiveXProgId(functionCall)` → retorna `"LasecHartCommDTM.ConfigControl"` ✅
4. PACTware faz `CoCreateInstance("LasecHartCommDTM.ConfigControl")` → ConfigControl é criado ✅
5. ConfigControl constructor roda OK, `OnHandleCreated()` OK, `LoadCurrentValues()` OK ✅
6. PACTware **repete** passos 3-5 (cria SEGUNDA instância) ❓
7. PACTware mostra diálogo "Ação do PACTware ativa. Por favor espere." por ~2 minutos
8. Depois chama `PrepareToRelease()` (timeout)

### Log exato do último teste
```
19:14:28.148 QI for {036d1489} (unknown)       ← IDtmChannel, não implementado
19:14:30.180 QI for {036d1480} (IDtmActiveXInformation) → HANDLED
19:14:30.220 QueryActiveXProgId → LasecHartCommDTM.ConfigControl
19:14:30.536 ConfigControl() constructor START
19:14:30.548 ConfigControl() constructor END
19:14:30.591 ConfigControl.OnHandleCreated()
19:14:30.597 ConfigControl.LoadCurrentValues() OK
19:14:32.455 QI for {036d1480} → HANDLED        ← segunda vez
19:14:32.465 QueryActiveXProgId → LasecHartCommDTM.ConfigControl
19:14:32.476 ConfigControl() constructor START   ← segunda instância
19:14:32.494 ConfigControl.LoadCurrentValues() OK
19:16:13.234 PrepareToRelease()                  ← timeout após ~2 min
```

### Diagnóstico: por que não funciona

O problema é que **um UserControl .NET exposto via COM NÃO implementa automaticamente as interfaces OLE de embedding**, que são necessárias para PACTware embutir o controle na janela MDI:

- **IOleObject** — PACTware precisa para SetClientSite, DoVerb(INPLACEACTIVATE)
- **IOleInPlaceObject** — PACTware precisa para InPlaceDeactivate, SetObjectRects
- **IOleInPlaceActiveObject** — PACTware precisa para TranslateAccelerator
- **IOleControl** — PACTware precisa para GetControlInfo, OnAmbientPropertyChange
- **IViewObject/IViewObject2** — PACTware precisa para Draw

**IMPORTANTE**: `System.Windows.Forms.Control` (classe base de UserControl) **implementa todas essas interfaces internamente** (`IOleObject`, `IOleInPlaceObject`, etc.). Porém, o CCW (COM Callable Wrapper) do .NET pode não expor corretamente essas interfaces quando o controle é criado via `CoCreateInstance` por um container nativo como PACTware.

O CWHart (CodeWrights) é um OCX nativo C++/ATL que implementa essas interfaces nativamente — por isso funciona.

### Abordagem IDtmApplication (alternativa testada)
Quando se implementa `IDtmApplication` ao invés de `IDtmActiveXInformation`:
- PACTware QI `{036D1480}` → NotHandled
- PACTware QI `{036D147E}` (IDtmApplication) → HANDLED
- PACTware chama `StartApplication(invokeId, functionCall, applicationState)`
- DTM abre seu **próprio** diálogo (janela separada — NÃO embedded no PACTware)
- **Funciona tecnicamente**, mas o diálogo é separado (não dentro do MDI do PACTware)
- O usuário NÃO quer essa abordagem — quer embedded como CWHart

---

## O QUE PRECISA SER FEITO

### Problema principal: fazer o ConfigControl funcionar como ActiveX embedded

O .NET Framework `Control` class implementa IOleObject etc. internamente via a classe `ActiveXImpl`. Para que o OLE InPlace funcione:

#### Opção A: Forçar o .NET ActiveX hosting (mais promissora)
O `System.Windows.Forms.Control` tem um mecanismo interno chamado `ActiveXImpl` que é ativado quando:
1. O controle recebe um `IOleClientSite` via `IOleObject.SetClientSite()`
2. O container chama `IOleObject.DoVerb(OLEIVERB_INPLACEACTIVATE)`

Pode ser que o problema seja:
- O HWND está sendo criado **antes** do OLE InPlace ser negociado (o log mostra `OnHandleCreated` imediatamente após o constructor)
- O `[ClassInterface(ClassInterfaceType.AutoDual)]` no ConfigControl pode interferir com a exposição das interfaces OLE

**Coisas para tentar:**
1. Mudar ConfigControl para `ClassInterfaceType.None` (pode deixar IOleObject ser encontrado pelo QI)
2. Não criar child controls no construtor — adiar para `OnHandleCreated` ou `OnPaint`
3. Verificar se PACTware consegue fazer QI para IOleObject no ConfigControl. Adicionar logging interceptando QI no ConfigControl
4. Testar se `AxHost`-based approach funciona — criar wrapper AxHost

#### Opção B: Implementar IOleObject manualmente no ConfigControl
Implementar explicitamente `IOleObject`, `IOleInPlaceObject`, `IOleInPlaceActiveObject` no ConfigControl, delegando para a implementação interna do `Control`. Isso é muito trabalhoso (~30+ métodos).

#### Opção C: Criar um OCX wrapper nativo em C++/ATL
Criar um projeto ATL simples que:
1. É registrado como ActiveX control
2. No DoVerb, cria o ConfigControl .NET via COM interop
3. Reparenta o HWND do ConfigControl como filho do OCX

Isso garante que PACTware veja um ActiveX nativo real.

#### Opção D: Usar a classe AxHosting do .NET (experimental)
Usar `System.Windows.Forms.AxHost` ou `WebBrowser`-style hosting para criar um controle que automaticamente suporta OLE InPlace.

### Investigações recomendadas
1. **Interceptar QI no ConfigControl**: Fazer ConfigControl implementar `ICustomQueryInterface` e logar todas as queries. Isso mostra se PACTware está conseguindo pedir IOleObject ou não.
2. **Verificar o registro**: Usar `OleView.exe` (ferramenta do SDK) para inspecionar o ConfigControl e ver se as interfaces OLE são listadas.
3. **Testar ClassInterfaceType.None**: Pode ser que `AutoDual` esteja impedindo a exposição de IOleObject.

---

## Arquivos Importantes

### `src/LasecHartCommDTM/CommDtm.cs` (~1000 linhas)
- Classe principal: `CommDtm`
- Implementa: IDtmInformation, IDtm, IFdtCommunication, IDtmParameter, IDtmActiveXInformation, IPersistStreamInit, IPersistPropertyBag, ICustomQueryInterface
- Define interfaces locais: IPersistStreamInit, IPersistPropertyBag, IDtmActiveXInformation
- `Log()` estático escreve em `C:\ProgramData\...\LasecHartDTM.log`
- `GetFunctions()` retorna XML das funções
- `QueryActiveXProgId()` retorna o ProgId do ConfigControl
- `InvokeFunctionRequest()` parseia functionId e abre dialog (não usado atualmente quando IDtmActiveXInformation está ativo)

### `src/LasecHartCommDTM/ConfigControl.cs` (~260 linhas)
- UserControl WinForms para tela de configuração
- GUID: `{B4B6B3E7-639D-460B-B9A0-6C7F7EB20030}`
- ProgId: `LasecHartCommDTM.ConfigControl`
- Campos: IP Address, IP Port, Protocol (udp/tcp), Primary Master, Preamble Count, Retry Count, Scan Start/Stop, Burst Mode, Timeout
- Botão Apply chama `CommDtm.ApplyConfiguration()`
- `[ComRegisterFunction]` cria chaves ActiveX: Control, Insertable, MiscStatus, TypeLib, VERSION, Implemented Categories

### `src/LasecHartCommDTM/DtmView.cs`
- Form (diálogo) para configuração — usado pelo InvokeFunctionRequest/StartApplication
- Mesmos campos que ConfigControl

### `src/LasecHartCommDTM/FdtInterfaces/`
- IDtm.cs, IDtmInformation.cs, ICommChannel.cs, IDtmView.cs, IFdtCommunicationEvents.cs, IFdtContainer.cs

### `src/HartEngine/`
- ChannelManager.cs, SerialChannel.cs, UdpChannel.cs, IChannel.cs
- Motor de comunicação HART

### `deploy_admin.ps1`
- Script de deploy: copia DLL, regasm, registra ActiveX, limpa log

---

## Registro COM do ConfigControl (estado atual)

Chaves que devem existir em `HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{B4B6B3E7-639D-460B-B9A0-6C7F7EB20030}`:
```
(Default) = "LasecHartCommDTM.ConfigControl"
\Control                         ← marca como ActiveX
\Insertable                      ← necessário para alguns containers
\InprocServer32
  (Default) = "mscoree.dll"
  Assembly = "LasecHartCommDTM, Version=..."
  Class = "LasecHartCommDTM.ConfigControl"
  CodeBase = "file:///C:\Program Files (x86)\PACTware 5.0\DTM700\..."
  RuntimeVersion = "v4.0.30319"
  ThreadingModel = "Both"
\MiscStatus
  (Default) = "0"
  \1
    (Default) = "131473"         ← OLEMISC flags
\TypeLib
  (Default) = "{40498B38-0A79-3F68-905F-7954AA76AEA6}"
\VERSION
  (Default) = "1.0"
\Implemented Categories
  \{40FC6ED4-2438-11CF-A3DB-080036F12502}  ← CATID Controls
  \{40FC6ED5-2438-11CF-A3DB-080036F12502}  ← Automation Objects
```

---

## Fluxo do PACTware (observado via log)

### Inicialização (ao expandir o DTM na árvore)
```
1. CoCreateInstance(CommDtm CLSID)
2. QI IPersistStreamInit → InitNew()
3. QI IDtm → Environment(), InitNew(), SetLanguage(), Config()
4. PrivateDialogEnabled(True)
5. QI IDtmParameter → GetParameters(), SetParameters(), GetParameters()
```

### Ao clicar em "parâmetro" (fdtConfiguration)
```
6. QI IDtm → GetFunctions(operationPhase)
7. QI {036D1489} IDtmChannel (não implementado — retorna NotHandled)
8. QI {036D1480} IDtmActiveXInformation → HANDLED
9. QueryActiveXProgId(functionCall XML) → "LasecHartCommDTM.ConfigControl"
10. CoCreateInstance(ConfigControl) → constructor, OnHandleCreated, LoadCurrentValues
11. [PACTware tenta OLE InPlace activation — FALHA]
12. Repete 9-10 (segunda tentativa)
13. [Timeout ~2 min] → PrepareToRelease()
```

### functionCall XML que PACTware envia
```xml
<?xml version="1.0"?>
<FDT xmlns="x-schema:DTMFunctionCallSchema.xml"
     xmlns:func="x-schema:DTMFunctionsSchema.xml"
     xmlns:appId="x-schema:FDTApplicationIdSchema.xml"
     xmlns:ops="x-schema:FDTOperationPhaseSchema.xml">
  <FDTFunctionCall func:functionId="1" ops:operationPhase="notSupported">
    <appId:ApplicationId applicationId="fdtConfiguration" />
  </FDTFunctionCall>
</FDT>
```

---

## Resumo Final

| Item | Status |
|------|--------|
| DTM aparece no PACTware | ✅ Funciona |
| Menu de funções aparece | ✅ Funciona |
| Parâmetros salvos/restaurados | ✅ Funciona |
| COM registration completo | ✅ Funciona |
| IDtmActiveXInformation respondida | ✅ Funciona |
| ConfigControl criado pelo PACTware | ✅ Funciona |
| ConfigControl **embutido** no PACTware | ❌ **NÃO FUNCIONA** |
| Comunicação HART real | 🔜 Após resolver UI |

**O blocker #1 é: fazer o ConfigControl .NET ser embutido como ActiveX dentro da janela MDI do PACTware, igual ao CWHart (OCX nativo).**
