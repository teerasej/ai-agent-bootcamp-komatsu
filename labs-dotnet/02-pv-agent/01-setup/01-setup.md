---
lab:
    title: 'D3-M1: PV Agent — Workshop Architecture & Environment Setup (C#)'
    description: 'Understand the PV Agent architecture, set up a Microsoft Foundry project, deploy a model, and confirm a working .NET development environment using the Microsoft Agent Framework.'
    level: 200
    islab: true
---

# 1. D3-M1: PV Agent — Workshop Architecture & Environment Setup (C#)

In this exercise, you'll set up everything you need to build a **Payment Voucher (PV) Agent** throughout the workshop using C# and the Microsoft Agent Framework. You'll create a Microsoft Foundry project, deploy a GPT-4.1 model, configure your .NET environment, and run a smoke test.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 1.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Describe the PV Agent architecture and its boundaries
2. Create and configure a Microsoft Foundry project
3. Deploy a GPT-4.1 model via the Foundry portal
4. Connect VS Code to your Foundry project using the Microsoft Foundry extension
5. Configure runtime settings with `appsettings.json`
6. Confirm that the deployed model is available and responding using the Microsoft Agent Framework for C#

## 1.2. Prerequisites

Before starting this exercise, ensure you have:

- An [Azure subscription](https://azure.microsoft.com/free/) with access to the resource group provided for this workshop
- [Visual Studio Code](https://code.visualstudio.com/) installed on your local machine
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later installed
- [Git](https://git-scm.com/downloads) installed on your local machine
- The **Microsoft Foundry** VS Code extension installed (covered in the steps below)
- The [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) VS Code extension installed

## 1.3. Scenario

You are joining the **PV Agent** workshop. Before writing any code, you need a working development environment connected to Microsoft Foundry. The PV Agent you'll build will:

- **Collect** payment voucher information from users through natural conversation
- **Validate** data against PV rules before submission
- **Produce** a valid PV JSON document ready for the back-office system

The agent does **not** approve requests, change budget limits, or interact directly with finance systems — it is a **data assistant**, not an approver.

---

## 1.4. Part 1: Understand the PV Agent Architecture

Before setting up any infrastructure, take a moment to understand what you're building.

### 1.4.1. PV Agent Boundaries

The PV Agent sits between the user and the PV system:

```
User (requestor)
    ↓  natural language input
PV Agent (Microsoft Agent Framework + Azure AI Foundry)
    ↓  structured PV JSON
Validation Layer
    ↓  if valid
PV System / Back-office (mock)
    ↓
Confirmation to user
```

**What the agent DOES:**
- Guides the user through collecting all required PV fields
- Asks clarifying questions for missing or ambiguous data
- Validates field values (enums, amounts, required fields)
- Produces a structured JSON payload

**What the agent does NOT do:**
- Approve or reject vouchers
- Change budgets or project allocations
- Access live financial systems directly (in this workshop)

### 1.4.2. Minimal PV Data Contract

The agent will collect and produce a JSON object following this structure:

```json
{
  "pv": {
    "pvTitle": "string",
    "requestDate": "YYYY-MM-DD",
    "requestor": { "name": "string" },
    "payee": { "name": "string" },
    "purpose": {
      "for": "string",
      "objective": "string"
    },
    "expense": {
      "type": "MonthlyFee | OneTime",
      "amount": { "value": 0, "currency": "THB" }
    },
    "project": {
      "projectName": "string",
      "budgetSummary": {
        "monthlyBudget": 0
      }
    },
    "approval": { "approverName": "string", "status": "Pending" },
    "status": "Draft | ReadyForSubmission"
  }
}
```

> **Key enums to remember:**
> - `expense.type`: `MonthlyFee` or `OneTime`
> - `approval.status`: `Pending` or `Approved`
> - `status`: `Draft` or `ReadyForSubmission`

---

## 1.5. Part 2: Create a Foundry Project

Now let's create the Microsoft Foundry project that will host your PV Agent throughout this workshop.

### 1.5.1. Create your Foundry project

1. In a web browser, open the [Foundry portal](https://ai.azure.com) at `https://ai.azure.com` and sign in using your Azure credentials.

1. Close any welcome panes or quick-start overlays. Use the **Foundry** logo at the top left to navigate to the home page if needed.

    > **Important**: Ensure you are using the **New** Foundry experience.

1. Select **Start building** from the top banner.

1. When prompted, select **Create a new project** and enter the following settings:
    - **Project name**: `[your-username]-pv-agent`  (for example, `teerasej-pv-agent`)

1. Expand **Advanced options** if needed to verify the region is set correctly.
   - **Resource Group**: Select the resource group assigned to you for this workshop
    - **Region**: **East US 2**

    > **Note**: Use East US 2 to ensure GPT-4.1 quota availability for all workshop participants.

2. Select **Create** and wait for the project to be provisioned. This may take 1–2 minutes.

3. When the project is created, you'll land on your project overview page. Keep this browser tab open.

---

## 1.6. Part 3: Deploy a GPT-4.1 Model

With your project created, you'll now deploy the model your agent will use.

### 1.6.1. Discover and deploy GPT-4.1

1. In the top navigation of the Foundry portal, select **Discover** > **Models**.

1. In the Model Catalog search bar, type `gpt-4.1` and press Enter.

1. Select the **GPT-4.1** model from the results.

1. Select **Deploy** (or **Deploy model**) and keep all default settings:
    - **Deployment name**: `gpt-4.1`
    - **Deployment type**: `Global Standard`
    - **Tokens per minute**: Leave as default

1. Select **Deploy** to confirm and wait for the deployment to complete.

1. Once deployed, the model will appear under **Models** in your project.

### 1.6.2. Get the API key and endpoint

1. Go to your project's home page in the Foundry portal.

1. Copy the **Project endpoint URL** — you'll use it in your `appsettings.json` file shortly.

1. Navigate to **Management** > **Keys and Endpoint** (or check the project settings).

1. Copy one of the **API keys** — you'll use this for authentication instead of `az login`. This saves time during each exercise test run.

---

## 1.7. Part 4: Install the Microsoft Foundry VS Code Extension

You'll interact with your Foundry project directly from VS Code throughout this workshop.

> **Skip this section** if you already have the Microsoft Foundry extension installed and signed in.

### 1.7.1. Install the extension

1. Open **Visual Studio Code** on your local machine.

1. Open the Extensions panel (**Ctrl+Shift+X** / **Cmd+Shift+X** on Mac).

1. Search for **Microsoft Foundry** and select the extension published by Microsoft.

1. Click **Install** and wait for the installation to complete.

1. Once installed, the extension icon will appear in the primary sidebar on the left.

### 1.7.2. Sign in and connect to your project

1. Click the **Microsoft Foundry** extension icon in the sidebar.

1. In the Resources view, select **Sign in to Azure...** and complete the authentication flow.

1. After signing in, expand your subscription and locate your Foundry resource.

1. Find the project you created (`[your-username]-pv-agent`), right-click it, and select **Set as active project**.

1. Expand the project and verify that you can see your deployed `gpt-4.1` model listed under **Models**.

---

## 1.8. Part 5: Prepare Your Development Environment

You'll now set up the local code repository and configure the .NET project.

### 1.8.1. Clone the starter repository

1. In VS Code, open the Command Palette (**Ctrl+Shift+P** / **Cmd+Shift+P**).

1. Select **Git: Clone** and enter the repository URL provided by your instructor.

1. Choose a local destination folder and select **Open** when prompted.

1. In the Explorer pane, navigate to the folder for this exercise:

    ```
    labs-dotnet/02-pv-agent/01-setup/Labfiles/
    ```

1. Right-click the folder and select **Open in Integrated Terminal**.

### 1.8.2. Restore NuGet packages

1. In the integrated terminal, run the following commands to restore and build the project:

    **macOS / Linux:**
    ```bash
    dotnet restore
    dotnet build
    ```

    **Windows (PowerShell):**
    ```powershell
    dotnet restore
    dotnet build
    ```

    > **Tip**: Make sure .NET 8.0 SDK (or later) is installed. You can verify with `dotnet --version`.

### 1.8.3. Configure application settings

1. In the `Labfiles/` folder, open the `appsettings.json` file.

1. Fill in the values:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "<your-project-endpoint>",
        "DeploymentName": "gpt-4.1",
        "ApiKey": "<your-api-key>"
      }
    }
    ```

    To get your **Endpoint** and **API Key**:
    - In the Foundry portal, go to your project's home page.
    - Copy the **Project endpoint URL** and paste it as the value for `Endpoint`.
    - Navigate to the project's **Keys and Endpoint** section and copy a key for `ApiKey`.

1. Save the file (**Ctrl+S**).

    > **Security Note**: Do not commit `appsettings.json` with real keys to source control. Add it to `.gitignore` in production scenarios.

---

## 1.9. Part 6: Smoke Test

Let's confirm that your environment is working before moving to the next module.

### 1.9.1. Complete the smoke test code

Before running the smoke test, complete the TODO sections in `Program.cs`.

1. In the `Labfiles/` folder, open `Program.cs`.

1. Find the comment `// Add references` and add these using statements at the top of the file:

    ```csharp
    using Azure;
    using Azure.AI.OpenAI;
    using Microsoft.Agents.AI;
    using OpenAI.Chat;
    ```

1. Find the comment `// Create and run the agent to test the connection` and add this block:

    ```csharp
    AIAgent agent = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
        .GetChatClient(deploymentName)
        .AsAIAgent(
            instructions: "You are a helpful assistant.",
            name: "SmokeTestAgent");

    var response = await agent.RunAsync(
        "Hello! Are you available? Please confirm you are working correctly.");
    ```

1. Find the comment `// Print the response and confirm success` and add this block:

    ```csharp
    Console.WriteLine($"Agent response: {response}");
    Console.WriteLine("\nSmoke test PASSED: Agent is working correctly!");
    ```

1. Save the file.

### 1.9.2. Test via VS Code extension

1. In the Microsoft Foundry extension sidebar, expand **Agents** under your project.

1. If no agents exist yet, that's expected — you'll create one in the next module.

1. Expand **Models** and confirm your `gpt-4.1` deployment is listed and shows as **Succeeded**.

### 1.9.3. Test via .NET CLI

1. In the terminal, run the smoke test:

    ```bash
    dotnet run
    ```

1. You should see output similar to:

    ```
    Testing connection to Foundry project...
    Endpoint: https://...
    Model: gpt-4.1

    Agent response: Hello! I'm working correctly and ready to help you.

    Smoke test PASSED: Agent is working correctly!
    ```

    > **Tip**: If you see a connection error, double-check the `Endpoint` and `ApiKey` in your `appsettings.json` file.

    > **Tip**: If you see an authentication error (401), verify that the API key is correct and that your Foundry project has the GPT-4.1 model deployed.

---

## 1.10. Summary

In this exercise, you:

- Reviewed the PV Agent architecture and its role boundaries
- Created a Microsoft Foundry project in East US 2
- Deployed a GPT-4.1 model to your project
- Connected VS Code to your Foundry project via the extension
- Configured your `appsettings.json` with the project endpoint, model name, and API key
- Created an AI agent using the Microsoft Agent Framework (`Microsoft.Agents.AI.OpenAI`)
- Ran a smoke test to confirm connectivity

You are now ready to design and build the PV Agent in the next module (**D3-M2**).

---

## 1.11. Clean Up

> **Do not delete your project yet** — you'll use it throughout the workshop (D3-M1 through D4).

If you need to restart this exercise from scratch, you can delete the project from the Foundry portal and repeat the steps above.
