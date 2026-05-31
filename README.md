# GitExtensions.ZimerfeldTree

Plugin para [GitExtensions](https://gitextensions.github.io/) que exibe branches **hierarquicamente** em estrutura de árvore, mostrando branches filhas.

**Versão atual: 1.0.95**

TreeOfLife

· · ← frutos dourados (apical)
\/ ← bifurcação apical
|
· /|\ · ← galho nível 3 + frutos
/ | \
 ·/ \|/ \· ← galho nível 2 + frutos
\ | /
· \|/ · ← galho nível 1 + frutos
⊙ ← "coração da vida" (fruto dourado central)
|
·/|\· ← raízes nível 1 + 2
|
───────────── ← dentro de um círculo verde
Elemento Cor Detalhe
Círculo verde-escuro (#145A29) fundo verde-claro #E8F5E9
Tronco verde-escuro 2 px, caps arredondados
Galhos (3 níveis) verde-escuro 1.5 → 0.9 px, afinando para o topo
Raízes (2 pares) verde-escuro 1.2 → 0.9 px
Frutos/folhas dourado (#D4A017) círculos 1.5 px nos galhos, 1.0 px nas raízes
Coração central dourado + borda verde círculo 2.2 px em (16,15)

---

## Funcionalidades

### Visualização hierárquica de branches

- Janela não-modal que permanece aberta em paralelo ao GitExtensions
- Árvore dividida em três seções fixas: **LOCAL**, **REMOTES** e **TAGS**
- **LOCAL e REMOTES** combinam **ancestralidade** (parentesco real por commits / organização GitFlow) com **agrupamento por caminho** (`/`): dentro de cada nível pai, nomes com `/` viram nós-pasta. Ex.: `feature/teste` aparece como pasta `feature` → folha `teste`, e `release/2026` como `release` → `2026`. Quando `feature/*` é filha de `develop`, fica `develop` → `feature` → `teste`
- **TAGS** também agrupa por `/` (sem ancestralidade)
- LOCAL, REMOTES e TAGS exibe `(nenhuma branch local encontrada)` quando não há branches
- A janela abre **centralizada na tela** (horizontal e vertical)
- Janela **redimensionável** com botões **Minimizar**, **Maximizar** e **Fechar** padrão do Windows (`Sizable`)
- A janela é **independente** do GitExtensions: minimizar o GitExtensions não afeta a janela ZimerfeldTree
- **Carregamento assíncrono**: ao abrir, a janela exibe o esqueleto imediatamente e depois mostra um **painel de progresso centralizado** ("Carregando dados do repositório") com barra de porcentagem (0→100%) enquanto lê os dados do repositório em background; a árvore é populada apenas ao final
- **Montagem da hierarquia otimizada**: o cálculo de parentesco entre branches usa um único `git log --all` para construir o grafo de commits em memória e determina os pais via BFS — complexidade O(commits) em vez do anterior O(N² × subprocesso), eliminando o gargalo em repositórios com dezenas ou centenas de branches
- **Overlay em toda atualização**: o painel de progresso aparece sempre que a árvore é recarregada — abertura inicial, checkout, nova branch, merge, rename, delete, GitFlow, refresh manual e troca de repositório
- **Lista de passos (somente leitura)**: o overlay exibe uma lista acumulativa de cada etapa executada ("Carregando branches locais…", "Calculando hierarquia…", etc.) — cada passo é adicionado à lista conforme é iniciado, permitindo acompanhar o progresso em detalhe
- **Botão Cancelar no overlay**: permite abortar o carregamento a qualquer momento (o cancelamento ocorre entre as etapas git, preservando os dados anteriores na árvore)
- **Formulário bloqueado durante carregamento**: todos os campos e botões ficam desabilitados enquanto o overlay está ativo e são reativados ao término (ou ao cancelar)
- **Botão "Fechar"** centralizado horizontalmente na parte inferior da janela (atalho: tecla **Esc**)

### Seletor de Working Directory e Branch

- **Linha "Working Directory:"** no topo da janela contém:
  - Label fixo `Working Directory:`
  - **ComboBox** (somente seleção) populado automaticamente com todos os repositórios listados no dashboard do GitExtensions (lido de `%APPDATA%\GitExtensions\GitExtensions\GitExtensions.settings`) e quando novo repositório é criado
  - Label `Branch: <nome>` mostrando a branch em checkout no repositório exibido
- Selecionar outro repositório no dropdown recarrega a árvore para aquele repositório
- A lista do combo é recarregada automaticamente sempre que o GitExtensions troca de repositório
- A branch atual aparece destacada com **texto em negrito** e cor de seleção do sistema (`[nome]`)
- Seções da árvore mostram contadores: `LOCAL (N)`, `REMOTES (N)`, `TAGS (N)`
- Status bar inferior mostra: `Local: N  |  Remoto: N  |  Tags: N`

### Filtro em tempo real

- Campo de pesquisa filtra branches em todas as seções simultaneamente
- Filtro preserva nós pai que possuem filhos correspondentes

### Botões Pull / Push / Commit / GitFlow

Exibidos acima da árvore quando há uma branch em checkout:

- **Pull** — executa `git pull --tags`: traz commits da branch rastreada **e** todas as tags do remoto, garantindo que tags de releases criadas em outras máquinas apareçam na seção TAGS
- **Push** — executa `git push` para a branch atual; o botão mostra `Push ↑N` quando há commits locais pendentes
- **Commit** / **Commit (N)** — abre a janela de Commit nativa do GitExtensions; o contador `(N)` só aparece quando há alterações pendentes; sem alterações o botão e o item do menu de contexto mostram apenas `Commit`
- **GitFlow** — abre a janela de operações GitFlow; disponível a qualquer momento, independentemente do estado do painel de aviso

### Persistência de estado da árvore

- O estado de expansão/recolhimento de cada nó é **salvo automaticamente** por Working Directory
- Ao abrir o plugin ou ao atualizar a árvore, a estrutura é restaurada exatamente como estava na última sessão
- Estado gravado em `%APPDATA%\GitExtensions\ZimerfeldTree.treestate.json`
- Salvamento com debounce de 500 ms para não gerar I/O excessivo durante expansões rápidas
- Primeira abertura de um repositório usa o comportamento padrão: LOCAL totalmente expandido, REMOTES e TAGS com apenas a raiz expandida
- Durante filtro ativo, todos os nós são expandidos automaticamente para mostrar os resultados

### Organização automática como GitFlow

- O plugin verifica se a hierarquia real (por parentesco de commits) respeita as regras do GitFlow:
  `master`/`main` na raiz, `develop` filho de `master`, e branches `feature/*`, `release/*` e `hotfix/*` nos pais esperados
- Quando detecta que a hierarquia está **fora da condição de GitFlow**, ele **aplica automaticamente** a organização GitFlow na árvore e exibe o aviso correspondente
- Nesse estado, o botão do painel de aviso mostra **"Restaurar hierarquia real"** — clicar nele volta a exibir a ancestralidade real do git
- A escolha manual do botão é respeitada e só é reavaliada ao trocar de repositório (ou reabrir a janela)

### Atualização automática

- A árvore é recarregada automaticamente ao:
  - Trocar de branch (**checkout**)
  - Trocar de repositório na UI do GitExtensions
  - Inicializar/reabrir um repositório
- Botão **Atualizar** para recarga manual

### Menu de contexto (botão direito)

| Item                    | Disponível para                                                                     |
| ----------------------- | ----------------------------------------------------------------------------------- |
| Commit (N)              | Sempre — abre a janela de Commit do GitExtensions; `N` = nº de alterações pendentes |
| Checkout                | Local, remota, tag                                                                  |
| Nova branch daqui…      | Local, tag                                                                          |
| Mesclar na branch atual | Local                                                                               |
| Rebase na branch atual  | Local                                                                               |
| Renomear…               | Local                                                                               |
| Excluir…                | Local, remota, tag                                                                  |
| GitFlow…                | Branch (local/remota/tag)                                                           |
| Expandir tudo           | Sempre                                                                              |
| Recolher tudo           | Sempre                                                                              |
| Atualizar               | Sempre                                                                              |

O item **Commit** mostra entre parênteses a quantidade de mudanças pendentes na working tree (arquivos staged, modificados e não rastreados), recalculada toda vez que o menu é aberto. Ao clicar, abre a janela de Commit nativa do GitExtensions **no processo já em execução**, de modo que todos os plugins de Commit Templates (ex.: *Zimerfeld: Auto-resumo*) já estejam carregados e visíveis no dropdown. Quando o repositório exibido no ZimerfeldTree divergir do repositório ativo no GitExtensions, a janela é aberta via novo processo como fallback.

Os separadores do menu de contexto são ocultados automaticamente quando todos os itens do grupo correspondente estão escondidos — sem linhas de separação órfãs.

### Janela GitFlow — comportamento geral

- Ao fechar a janela GitFlow, a janela ZimerfeldTree é reposicionada automaticamente ao **centro da tela**
- Após um **Start** bem-sucedido, o painel "Manage existing branches" é pré-selecionado automaticamente no mesmo **Type** e na branch recém-criada — válido para feature, release, hotfix, bugfix e support

### Janela GitFlow — branch base no Start

No painel **Start branch** da janela GitFlow, além de tipo e nome, há a opção **based on:**:

- Por padrão o dropdown fica **desabilitado** e usa `develop` como base (comportamento padrão do `git flow ... start`)
- Ao marcar o checkbox **based on:**, o dropdown é habilitado e lista as branches locais, permitindo iniciar a nova branch a partir de outra — por exemplo, uma **feature filha de outra feature pai**
- A base escolhida é passada ao comando: `git flow feature start "<nome>" "<base>"`
- **Nome padrão de release**: ao selecionar o tipo **release**, o campo de nome é preenchido automaticamente com a convenção `yyyyMMddHHmm` (ex.: `202605311230`), gerando branches como `release/202605311230`; o preenchimento só ocorre quando o campo está vazio, nunca sobrescrevendo digitação manual

### Janela GitFlow — painel "Manage existing branches" (git-flow-next)

O painel foi adaptado ao **git-flow-next**, que não possui o comando `pull` nem as flags `-S`/`-p` do finish:

- **Publish** — `git flow <tipo> publish "<nome>"`: envia a branch para o remoto
- **Track** — `git flow <tipo> track "<nome>"`: cria uma branch local que rastreia a branch remota correspondente (útil para branches iniciadas por outra pessoa)
- **Update** — `git flow <tipo> update "<nome>"`: traz as mudanças da branch **pai** (ex.: develop) para a branch
- **Finish** — `git flow <tipo> finish [-k] [--no-fetch] "<nome>"`: mescla de volta e remove a branch; o checkbox **Keep branch after finish** adiciona `-k` e o checkbox **No fetch (--no-fetch)** evita a busca remota
  - Antes de executar o finish, o plugin executa automaticamente `git fetch` para manter as branches de rastreamento locais sincronizadas com o remoto e evitar divergências; o fetch é omitido quando **No fetch** está marcado
  - **Auto-resolução de "merge in progress"**: quando o finish falha com `a merge is already in progress for branch '<tipo>/<nome>'`, o plugin aborta o estado travado e retenta o finish original automaticamente. A recuperação tem três níveis: ① `git flow <tipo> finish --abort` para limpar o lock do git-flow; ② `git merge --abort` para limpar o `MERGE_HEAD` do git; ③ **recuperação de deadlock** — o git-flow-next mantém um arquivo de estado persistente (`.git/gitflow/state/*.json`) que sobrevive mesmo quando o git não tem `MERGE_HEAD`; nesse caso os dois aborts falham e o plugin remove o arquivo órfão diretamente. Após a limpeza, volta à branch original e retenta o finish
  - A janela GitFlow mantém o foco após cada comando executado
- **Finish de `release` — fluxo completo automático**: quando o tipo é `release` e o checkbox **No fetch** não está marcado, o painel executa automaticamente em sequência (com as saídas anexadas à janela de resultado):
  1. `git push <remote> release/<nome>` — envia a release para o remoto **antes** do finish, evitando o erro `fatal: couldn't find remote ref release/<nome>` gerado pelo git-flow ao buscar a branch remota
  2. `git flow release finish [-k] "<nome>"`
  3. `git push <remote> <master>` (nome lido de `gitflow.branch.master`)
  4. `git push <remote> <develop>` (nome lido de `gitflow.branch.develop`)
  5. `git push <remote> refs/tags/<nome>` — envia a **tag** criada pelo finish ao remoto (o git flow só cria a tag localmente)
  6. `git push <remote> --delete release/<nome>` — remove a **branch remota** da release; o git-flow-next normalmente já a remove durante o finish, então um erro "remote ref does not exist" aqui é esperado e inofensivo (executado com `suppressError`)
  7. `git checkout <develop>`
  
  Ao concluir com sucesso, a seção **TAGS** da árvore é expandida automaticamente e o foco vai para o tag criado pelo finish.

  O remote usado é `origin` (ou o primeiro configurado quando `origin` não existe). Se um dos passos de push de master/develop falhar, o fluxo para naquele ponto e a mensagem de erro é exibida; o push da tag e a remoção da branch remota não interrompem o fluxo.

- O dropdown de branch lista **apenas as branches locais** do tipo selecionado, refletindo o que existe localmente; é recarregado após cada comando git flow (ex.: ao finalizar uma branch com Finish, ela é removida do dropdown automaticamente)
- Ao abrir a janela, se a branch em **checkout** corresponder a um tipo do git flow (ex.: `feature/manage`), o dropdown de tipo e o dropdown de branch já vêm pré-selecionados nesse tipo e nessa branch

#### Tratamento de erros

Quando um comando git flow falha, o resultado é exibido na janela e um aviso é mostrado. Se o erro indicar uma **branch base/produção ausente** (ex.: `couldn't find remote ref main`, `start point branch 'main' does not exist`), a mensagem orienta a verificar as branches existentes e a configuração `gitflow.branch.*`, e sugere marcar **No fetch** quando a falha for ao buscar do remoto.

### Ícone "Árvore da Vida"

O plugin usa um ícone gerado em tempo de execução via GDI+ (sem imagens externas ou recursos embutidos). O design é uma **Árvore da Vida** simétrica dentro de um círculo:

- **Círculo** com fundo verde-claro e borda verde-escura
- **Tronco** central vertical (verde-escuro, cantos arredondados)
- **Galhos** em 3 níveis + bifurcação apical — cada nível mais fino e mais estreito
- **Raízes** em 2 pares curvos abaixo do tronco
- **Frutos/folhas** dourados nos extremos de cada galho e raiz
- **"Coração da vida"** — fruto dourado central no tronco, representando a força vital

O ícone aparece:

- No **menu Plugins** do GitExtensions (16 × 16 px)
- Na **barra de título** da janela do plugin e na barra de tarefas do Windows (ICO multi-size: 32 + 16 px, formato PNG-encoded Vista+)

O arquivo [`TreeOfLifeIcon.cs`](src/GitExtensions.ZimerfeldTree/TreeOfLifeIcon.cs) contém toda a lógica de renderização. Não há dependências externas.

### Atalhos de teclado e mouse

- **Duplo clique** em qualquer branch → checkout da branch selecionada
- **Enter** → checkout da branch selecionada
- **Botão direito** → seleciona o nó e abre o menu de contexto

### Janela não-modal persistente

- A janela permanece aberta enquanto o GitExtensions está em uso
- Fechar a janela a destrói — necessário reabrir para recarregar dados
- Singleton: uma única instância por sessão do GitExtensions
- **Foco persistente após ações**: qualquer ação executada na janela (Pull, Push, Commit, Checkout, Nova branch, Merge, Rebase, Renomear, Excluir) devolve o foco à janela ZimerfeldTree ao concluir. Como a janela é independente (sem owner), notificar o GitExtensions para atualizar sua UI traria a janela principal do GitExtensions para frente; o plugin reativa a ZimerfeldTree logo em seguida. A **única exceção é o botão GitFlow**: ele abre a janela GitFlow (modal), que mantém o próprio foco enquanto estiver aberta

## Instalação

### Instalação Powershell/Terminal as Administrator

cd C:\GitExtensions\ZimerfeldTree\tools
.\install.ps1

### Instalação Manual

1. Copie `GitExtensions.Plugins.ZimerfeldTree.dll` para:
   ```
   C:\Program Files\GitExtensions\Plugins\
   ```
2. Reinicie o GitExtensions
3. O plugin aparece em **Plugins → ZimerfeldTree**

### Via NuGet (repositório local)

```powershell
Install-Package GitExtensions.ZimerfeldTree -Source C:\NUGET
```

---

## Hierarquia de branches — limitações

### A hierarquia é por nome, não por parentesco de commits

O plugin agrupa branches usando o separador `/` do nome — **não** pelo histórico de commits do git. `master` e `develop` são irmãos porque nenhum deles contém `/`:

```
LOCAL
├── develop      ← irmão
└── master       ← irmão
```

Para que uma branch apareça como filha de outra, o nome deve conter `/`:

```
LOCAL
└── feature/
    ├── login    ← feature/login
    └── pagamento ← feature/pagamento
```

### Branch real não pode ser nó pai de outra branch

O git armazena refs como arquivos no sistema de arquivos. Se `feature/login` já existe como branch, tentrar criar `feature/login/oauth` resulta em erro:

```
fatal: cannot lock ref 'refs/heads/feature/login/oauth':
'refs/heads/feature/login' exists; cannot create 'refs/heads/feature/login/oauth'
```

Isso ocorre porque `feature/login` seria simultaneamente um **arquivo** (a branch) e um **diretório** (pai de `oauth`), o que é impossível no sistema de arquivos.

**Solução:** use prefixos distintos ou nomes irmãos:

| Intenção                  | Nomes que funcionam                            |
| ------------------------- | ---------------------------------------------- |
| Sub-tarefas de login      | `feature/login-oauth`, `feature/login-session` |
| Agrupador sem branch real | `feature/login/base` + `feature/login/oauth`   |

### Gitflow não prevê feature filha de feature

O gitflow define uma hierarquia fixa onde todas as branches `feature/*` derivam de `develop` e são **irmãs** entre si. Sub-features são geralmente tratadas com commits separados na mesma branch ou com branches irmãs de prefixo comum.

---

## Uso

1. Abra um repositório no GitExtensions
2. Vá em **Plugins → ZimerfeldTree**
3. A janela de hierarquia fica aberta ao lado — navega, filtra, faz checkout sem sair dela

---

## Build

```powershell
# Incrementa versão, compila e gera .nupkg
# Execute como Administrador para também copiar o DLL para Plugins\
pwsh C:\NUGET\ZimerfeldTree\build.ps1
```

O script:

1. Lê a versão atual do `.nuspec` e incrementa o `build` (major.minor.**build**)
2. Atualiza `.nuspec` e `.csproj` com a nova versão
3. Compila em modo Release (`net9.0-windows`)
4. Se for Administrador, copia o DLL para `C:\Program Files\GitExtensions\Plugins\`
5. Empacota o `.nupkg` em `C:\NUGET\ZimerfeldTree\`

---

## Desinstalação

Delete o arquivo:

```
C:\Program Files\GitExtensions\Plugins\GitExtensions.Plugins.ZimerfeldTree.dll
```

O GitExtensions não é afetado pela remoção do plugin.

---

## Estrutura do projeto

```
ZimerfeldTree/
├── src/
│   └── GitExtensions.ZimerfeldTree/
│       ├── ZimerfeldTreePlugin.cs       # Ponto de entrada MEF (IGitPlugin)
│       ├── BranchHierarchyForm.cs       # Janela WinForms principal
│       ├── BranchHierarchyService.cs    # Lógica git: coleta, hierarquia, Git Flow
│       ├── BranchNode.cs                # Modelos: BranchNode, RemoteGroupNode, BranchTag
│       └── GitExtensions.ZimerfeldTree.csproj
├── build.ps1                            # Script de build e deploy
├── README.md                            # Este arquivo
└── GitExtensions.ZimerfeldTree.nuspec   # Metadados do pacote NuGet
```
