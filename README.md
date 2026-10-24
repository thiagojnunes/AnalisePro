# ⚡ AnalisePro - Monitor de Hardware, Diagnóstico de TI & Analisador de Discos

Aplicativo Windows leve, moderno e **100% portátil** (apenas um arquivo executável `.exe` de ~1.6 MB, sem instaladores, sem dependência de DLLs externas) com monitoramento completo de componentes e sensores em tempo real integrado a um **Analisador Visual de Espaço em Disco**, **Canivete Suíço de Rede & Wi-Fi** e **Testador de Portas Seriais RS-232 & Balanças Comerciais (Placas PCI-Express x1)**.

---

## 🚀 Como Executar

Basta dar um duplo-clique no executável:
- **`AnalisePro.exe`** (Tamanho: ~1.6 MB)

*Abre instantaneamente sem telas de bloqueio, rodando em modo nativo com sensores ACPI, cálculo de CPU em tempo real e interface responsiva com suporte a maximizar janela e alternância de temas.*

---

## 🌟 Recursos Principais

### 1. 🌓 Tema Escuro & Claro (Dark / Light Mode) e Janela Maximizável
* **Alternador no Topo:** Botão direto na barra de título para alternar entre **Tema Escuro (Dark)** e **Tema Claro (Light)** conforme a preferência e ambiente do técnico.
* **Maximizar / Restaurar:** Suporte completo a redimensionamento, botão de maximizar/restaurar e duplo clique na barra de título.

### 2. ⚡ Hardware (Processador & Placa-Mãe)
* **Processador:**
  * Marca e modelo exato (ex: *13th Gen Intel(R) Core(TM) i5-13420H*).
  * Codinome e litografia (ex: *Raptor Lake 10nm / Intel 7*).
  * Soquete físico (ex: *BGA / U3E1*).
  * Núcleos físicos e threads lógicas (ex: *8 Núcleos / 12 Threads*).
  * Frequência de clock base e clocks individuais.
  * Caches: L1, L2 (*7.0 MB*) e L3 (*12.0 MB Smart Cache*).
  * Conjunto de instruções suportadas: *x86-64, MMX, SSE4.2, AVX, AVX2, FMA3, AES, VT-x*.
  * Medidores de carga individuais em tempo real por núcleo.
* **Placa-Mãe (Motherboard):**
  * Fabricante da placa (ex: *LENOVO, ASUS, GIGABYTE*).
  * Modelo e produto (ex: *LNVNB161216*).
  * Versão e número de série da placa.
  * Informações de BIOS/UEFI: Fornecedor, Versão e Data de lançamento.

### 3. 🧠 Memória RAM (Detalhes SPD de Fábrica)
* Identificação de cada módulo instalado por slot (*Controller0-ChannelA*).
* Fabricante do chip de memória (ex: *Zilia, Micron, Samsung, Kingston*).
* Número de peça exato (*Part Number* do fabricante).
* Tecnologia da memória (*DDR5 / DDR4*).
* Frequência nominal homologada (*5600 MT/s*) e velocidade configurada na placa (*4800 MT/s*).
* Capacidade do pente e uso total em tempo real com arquivo de paginação (*Swap*).

### 4. 🎮 Placas de Vídeo (Múltiplas GPUs: Dedicada & Integrada)
* **Suporte Completo a Múltiplas GPUs:** Identifica e exibe simultaneamente placas dedicadas (*NVIDIA GeForce RTX / AMD Radeon RX*) e vídeo integrado (*Intel UHD / Iris Xe / AMD Radeon Graphics*).
* Versão do driver de vídeo instalado de cada placa.
* Resolução da tela e taxa de atualização nativa (ex: *1920 x 1080 @ 60 Hz*).
* Memória de vídeo (VRAM compartilhada e dedicada).
* Monitoramento ao vivo: Temperatura, carga de renderização D3D 3D, clock e voltagem.

### 5. 🥧 Analisador Visual de Espaço em Disco (Interativo)
* **Gráfico de Pizza / Donut Interativo:**
  * Desenho vetorial em tempo real com fatias coloridas proporcionais ao tamanho de cada pasta e arquivo.
  * *Tooltip* dinâmico ao passar o mouse com nome, tamanho formatado (GB/MB) e porcentagem.
  * **Clique para Entrar:** Clique em qualquer fatia ou dê duplo-clique na lista para entrar na pasta e ver o que está consumindo espaço!
* **Navegação Inteligente (Breadcrumbs):**
  * Seletor de unidade (*C:\, D:\*).
  * Barra de caminho navegável (*C:\ > Users > thiag...*).
  * Botão *"⬆️ Subir Pasta"* para voltar ao nível anterior.
  * Botão *"📁 Abrir"* para abrir a pasta diretamente no Windows Explorer.
  * Cálculo assíncrono em segundo plano sem travar a interface.

### 6. ⌨️ Periféricos & Dispositivos
* Ícones de alto contraste com destaque neon para todos os componentes.
* Dispositivos de Áudio instalados (Placas de som, microfones, áudio digital).
* Mouses, Touchpads e Teclados ativos.
* Discos rígidos físicos e SSDs NVMe com modelos de fábrica.
* Atalho de 1-clique para o **Testador de Portas Seriais & Balanças Comerciais**.

### 7. 🌐 Rede & Diagnóstico TI (Canivete Suíço de Rede)
* **Banner de Destaque de Conexão Gigabit (1000 Mbps):**
  * Detecta instantaneamente se a conexão está operando em **Gigabit (1000 Mbps Full-Duplex)** com os 4 pares (8 fios) íntegros.
  * Alerta de cabo em destaque visual caso a placa suporte Gigabit mas tenha conectado em apenas **100 Mbps**, apontando defeito nos pinos **4, 5, 7 ou 8** do RJ45 ou limitação de porta de switch.
* **Diagnóstico de Wi-Fi:**
  * Nome da Rede (SSID) e BSSID (MAC do ponto de acesso).
  * Força do sinal em porcentagem (%) com indicador visual colorido.
  * Canal e padrão de rádio (*802.11ax Wi-Fi 6, 802.11ac Wi-Fi 5*).
  * Criptografia e autenticação (*WPA2 / WPA3-Personal*).
  * Velocidade negociada de transmissão (Tx) e recepção (Rx).
* **Tempos de Ping Contínuos ao Vivo:**
  * Atualização automática periódica sem travar a tela dos tempos de resposta do **Gateway Local**, **WAN (1.1.1.1)** e **Web DNS (google.com)**.
* **Ações Rápidas de 1-Clique do Técnico:**
  * **[🔄 IP Release & Renew]**: Renova a concessão DHCP (`ipconfig /release` e `/renew`).
  * **[🧹 Limpar Cache DNS]**: Executa `ipconfig /flushdns` para limpar nomes corrompidos.
  * **[🔌 Reiniciar Adaptador]**: Desativa e reativa a interface de rede para destravar a placa.
  * **[⚠️ Resetar TCP/IP]**: Redefine o catálogo Winsock e a pilha TCP/IP padrão (`netsh winsock reset` e `netsh int ip reset`).
* **Scanner Completo de Rede Local (Network Discovery):**
  * Varredura paralela ultrarrápida da sub-rede local inteira (ex: `/24`, de `.1` até `.254`).
  * Tabela com: **Ícone destacado**, **Endereço IP**, **Nome do Dispositivo (Hostname)**, **Fabricante** (identificado pelo OUI do MAC), **Endereço MAC físico** (via `SendARP` nativo), **Latência (Ping)** e **Portas de Serviços Abertas** (Web 80, SMB 445, RDP 3389, Print 9100).

### 8. 🔌 Portas Seriais & Balanças Comerciais (Placas PCI-Express x1)
* **Diagnóstico de Hardware Serial (RS-232):**
  * Enumeração detalhada de portas COM via WMI e PnP.
  * Identificação automática de **Placas PCI / PCIe x1 Seriais** (detectando se o problema é o slot, placa física ou driver versus o dispositivo serial).
* **Comunicação com Balanças Comerciais de Checkout / PDV:**
  * Configuração flexível de Baud Rate (2400, 4800, 9600, 19200, 115200 bps), Data Bits, Paridade e Stop Bits.
  * **Controle de Hardware DTR & RTS:** Essencial para alimentar os circuitos de interface de balanças comerciais.
  * **Leitura de Pinos de Hardware:** Monitoramento em tempo real dos pinos **CTS**, **DSR** e **CD** para certificar se a placa PCI x1 está eletricamente conectada.
  * **Comandos de 1-Clique para Balanças:**
    * **⚖️ Toledo Prix (3, 4, 5):** Envia o byte ASCII `ENQ` (`0x05`) para requisição de peso.
    * **⚖️ Filizola / Elgin:** Envia o comando `P\r\n`.
    * **⚖️ Balmak / Urano:** Envia o comando `W\r\n`.
  * **Terminal de Recepção em Tempo Real:** Exibe os bytes recebidos simultaneamente em **ASCII** (com destaque de caracteres de controle `[STX]`, `[ETX]`, `[ENQ]`, `[ACK]`) e em **HEX**, com contador de bytes recebidos.

---

## 🛠️ Compilação

Para compilar o executável portátil único a partir do código-fonte:

```cmd
build.bat
```

Ou diretamente pelo MSBuild do Windows:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' AnalisePro.csproj /p:Configuration=Release
```
