# ⚡ AnalisePro - Monitor de Hardware & Analisador de Discos

Aplicativo Windows leve, moderno e **100% portátil** (apenas um arquivo executável `.exe` de ~1.5 MB, sem instaladores, sem dependência de DLLs externas) com monitoramento completo de componentes e sensores em tempo real integrado a um **Analisador Visual de Espaço em Disco**.

---

## 🚀 Como Executar

Basta dar um duplo-clique no executável:
- **`AnalisePro.exe`** (Tamanho: ~1.5 MB)

*Ele abre instantaneamente sem telas de bloqueio ou dependências adicionais, rodando em modo nativo com sensores ACPI e cálculo de CPU em tempo real.*

---

## 🌟 Recursos Principais

### 1. ⚡ Hardware (Processador & Placa-Mãe)
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

### 2. 🧠 Memória RAM (Detalhes SPD de Fábrica)
* Identificação de cada módulo instalado por slot (*Controller0-ChannelA*).
* Fabricante do chip de memória (ex: *Zilia, Micron, Samsung, Kingston*).
* Número de peça exato (*Part Number* do fabricante).
* Tecnologia da memória (*DDR5 / DDR4*).
* Frequência nominal homologada (*5600 MT/s*) e velocidade configurada na placa (*4800 MT/s*).
* Capacidade do pente e uso total em tempo real com arquivo de paginação (*Swap*).

### 3. 🎮 Placa de Vídeo (GPU)
* Nome e modelo completo da GPU (*Intel(R) UHD Graphics / NVIDIA GeForce / AMD Radeon*).
* Versão do driver de vídeo instalado.
* Resolução da tela e taxa de atualização nativa (ex: *1920 x 1080 @ 60 Hz*).
* Memória de vídeo (VRAM compartilhada e dedicada).
* Monitoramento ao vivo: Temperatura, carga de renderização D3D 3D, clock e voltagem.

### 4. 🥧 Analisador Visual de Espaço em Disco (Interativo)
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

### 5. ⌨️ Periféricos & Dispositivos
* Dispositivos de Áudio instalados (Placas de som, microfones, áudio digital).
* Mouses e Touchpads detectados.
* Teclados ativos.
* Discos rígidos físicos e SSDs NVMe com modelos de fábrica.
* Controladores de rede.

### 6. 📊 Monitoramento em Tempo Real
* **Cálculo de CPU Total:** Utiliza a API Win32 nativa `GetSystemTimes` do Windows, garantindo precisão idêntica ao Gerenciador de Tarefas do Windows (Task Manager).
* **Temperaturas:** Monitoramento de temperaturas da CPU, GPU e SSDs com código dinâmico de cores (Verde, Ciano, Laranja, Vermelho).
* **Rede:** Velocidade instantânea de download e upload (KB/s).

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
