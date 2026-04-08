# Changelog - Backup Driver v1.2

**Data:** 27/03/2026  
**Versão:** 1.2  
**Desenvolvedor:** Amelia (Senior Software Engineer)

## 🚀 Novas Funcionalidades

### 🔐 Sistema e Segurança
- **Instância Única (MUTEX):** Implementado controle rigoroso via `System.Threading.Mutex` nomeado. O aplicativo agora detecta se já existe uma instância em execução e aborta novas tentativas automaticamente, evitando conflitos de acesso a arquivos.
- **System Tray (Bandeja):** Integração nativa com a bandeja do Windows. O fechamento (X) do aplicativo agora minimiza para a bandeja e mantém o agendamento ativo.

### 📊 Dashboard Inteligente
- **Indicadores de Conectividade:** Novo painel visual no Dashboard mostrando em tempo real o status do Google Drive e Healthcheck (Ativo/Inativo).
- **Status do Agendador:** Exibição clara do estado do motor de agendamento (Rodando/Pausado/Parado).
- **Orquestração Resiliente:** Se o Google Drive estiver desabilitado, o backup conclui localmente com sucesso, notificando o usuário sem gerar erros críticos.

### ⏳ Controle de Fluxo
- **Pausar/Retomar:** Adicionados comandos na tela de agendamento que permitem suspender a execução automática sem perder as configurações salvas.
- **Feedback Visual:** Botões de pausa/retomada agora possuem estado visual (habilitado/desabilitado) conforme a situação do serviço.

### 🎨 Experiência do Usuário (UX)
- **Interface Premium:** Refatoração completa das telas de Dashboard e Agendamento com gradientes, bordas arredondadas e melhor espaçamento.
- **Responsividade:** Implementação de layouts baseados em Grids proporcionais e ScrollViewers, garantindo usabilidade em qualquer resolução ou tela cheia.
- **Alertas Centralizados:** Garantia técnica de que todas as janelas de diálogo (sucesso/erro) aparecem centralizadas em cima da aplicação.

## 🛠️ Mudanças Técnicas
- **Arquivos modificados:**
  - `src/BackupDriver.App/Program.cs`: Lógica de Mutex.
  - `src/BackupDriver.App/App.axaml`: Definição do TrayIcon via XAML.
  - `src/BackupDriver.App/App.axaml.cs`: Lógica de interceptação de fechamento e handlers de tray.
  - `src/BackupDriver.App/ViewModels/DashboardViewModel.cs`: Propriedades reativas para status.
  - `src/BackupDriver.App/Views/DashboardView.axaml`: Novo layout responsivo e indicadores.
  - `src/BackupDriver.App/ViewModels/ScheduleViewModel.cs`: Controle de CanPause/CanResume.
  - `src/BackupDriver.App/Views/ScheduleView.axaml`: Novo layout de agendamento.
  - `src/BackupDriver.App/BackupDriver.App.csproj`: Alinhamento do caminho de build (1.2).
  - `src/BackupDriver.App/Views/StatusColorConverter.cs`: Novo conversor para status visuais.
  - `src/BackupDriver.Core/Backup/BackupOrchestrator.cs`: Refinamento das mensagens de status "PULADO".

## ✅ Definição de Concluído (DoD)
- [x] Código compilado com sucesso.
- [x] Mutex validado.
- [x] Comportamento tray validado.
- [x] Layout responsivo testado em Maximize.
- [x] Output da build configurado para `\builds\Release\1.2`.
