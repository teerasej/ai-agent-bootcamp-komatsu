---
lab:
    title: 'D3-M2: PV Agent — Agent Role, Tasks, and Conversation Design (C#)'
    description: 'Design the PV Agent instruction, define the conversation flow for slot-filling, test with user stories in the Foundry portal, then implement a multi-turn conversation loop using the Microsoft Agent Framework in C#.'
    level: 200
    duration: 45
    islab: true
---

# 2. D3-M2: PV Agent — Agent Role, Tasks, and Conversation Design (C#)

In this exercise, you'll design the PV Agent's identity and conversation behavior. You'll write an agent instruction (system prompt), define a slot-filling conversation flow, test it with multiple user stories in the Foundry portal, then implement a **multi-turn conversation loop** in C# using `AgentSession` from the Microsoft Agent Framework SDK.

> **Note**: Some of the technologies used in this exercise are in preview or in active development. You may experience some unexpected behavior, warnings, or errors.

## 2.1. Learning Objectives

By the end of this exercise, you'll be able to:

1. Write a clear agent instruction (system prompt) that defines role, rules, and output format
2. Identify required and optional fields for a PV document
3. Design a slot-filling conversation flow in natural language
4. Test the agent with different user story patterns in the Foundry playground
5. Implement a multi-turn conversation loop in C# using `AIAgent` and `AgentSession` from the Microsoft Agent Framework

## 2.2. Prerequisites

- Completed **D3-M1** (Foundry project created, GPT-4.1 deployed, `appsettings.json` configured)
- Microsoft Foundry VS Code extension installed and connected
- .NET 8.0 SDK installed and `dotnet build` succeeded in D3-M1

## 2.3. Scenario

Before writing any code, you need to define what the PV Agent knows, what it can do, and how it talks to users. You'll approach this the way a product team would — by writing a clear instruction document and validating it with real conversation scenarios in the portal.

---

## 2.4. Part 1: Define the PV Agent Role

The agent instruction (system prompt) is the most important input to an AI agent. A well-designed instruction produces consistent, accurate, and safe behavior.

### Design the Conversation Flow

A good PV Agent follows a **slot-filling** pattern:

1. Greet the user and understand their intent
2. Identify what type of PV they need (`MonthlyFee` or `OneTime`)
3. Collect mandatory fields — ask **one question at a time** for missing fields
4. Confirm the summary with the user
5. Set `status = ReadyForSubmission` and output the final JSON

**Important design rules:**
- Never ask for a field the user already provided
- Never invent data that the user did not provide
- Always state your assumptions explicitly (e.g., "I'll use THB as the default currency")
- Only output the final JSON after user confirmation

---

## 2.5. Part 2: Create the Agent Instruction in the Foundry Portal

Now you'll create your PV Agent directly in the Foundry portal and write its instruction.

### 2.5.1. Create the agent

1. Open the [Foundry portal](https://ai.azure.com) and navigate to your `[username]-pv-agent` project.

1. In the left navigation, select **Build** > **Agents**.

1. Select **+ New agent**.

1. Set the **Agent name** to `pv-agent` and select **Create**.

1. The agent playground will open. The GPT-4.1 model should be selected automatically.

### 2.5.2. Write the agent instruction

1. In the **Instructions** field, paste the following instruction text:

    ```
    You are "PV Agent" — an AI assistant that helps requestors draft a Payment Voucher (PV) request.

    YOUR ROLE:
    - Guide the user through collecting all required PV fields via natural conversation
    - Ask only for missing critical fields, one at a time
    - State your assumptions explicitly (for example, "I'll use THB as the default currency")
    - Show a confirmation summary before marking the PV as ReadyForSubmission
    - Always output a valid JSON object as the final step

    REQUIRED FIELDS (must be collected before ReadyForSubmission):
    - pvTitle: Short, descriptive title for the voucher
    - requestDate: Date in YYYY-MM-DD format
    - requestor.name: Full name of the person making the request
    - payee.name: Full name or company of who will receive the payment
    - purpose.for: What specifically is being paid for
    - purpose.objective: The business reason or objective
    - expense.type: MUST be exactly "MonthlyFee" or "OneTime" — map user's words to one of these two values
    - expense.amount.value: A positive number — if user says text like "one thousand", convert to 1000
    - expense.amount.currency: Default to "THB" unless user specifies otherwise
    - project.projectName: Name of the project this payment belongs to — never invent, always ask user
    - approval.approverName: Name of the approver

    CONSTRAINTS:
    - You do NOT approve requests or change budget limits
    - You do NOT invent project names, payee names, or amounts
    - approval.status is always "Pending" for new requests
    - Set status to "ReadyForSubmission" only after user confirms the summary
    - Keep status as "Draft" otherwise

    OUTPUT FORMAT (produce this JSON when all fields are collected and confirmed):
    {
      "pv": {
        "pvTitle": "...",
        "requestDate": "YYYY-MM-DD",
        "requestor": { "name": "..." },
        "payee": { "name": "..." },
        "purpose": {
          "for": "...",
          "objective": "..."
        },
        "expense": {
          "type": "MonthlyFee | OneTime",
          "amount": { "value": 0, "currency": "THB" }
        },
        "project": {
          "projectName": "...",
          "budgetSummary": {
            "monthlyBudget": 0
          }
        },
        "approval": { "approverName": "...", "status": "Pending" },
        "status": "Draft | ReadyForSubmission"
      }
    }
    ```

1. Select **Save** (or **Apply**) to save the agent configuration.

---

## 2.6. Part 3: Test with User Stories in the Playground

Test your agent instruction using three different user stories. Observe how the agent handles each case.

### User Story 1 — Monthly Subscription (Happy Path)

Start a **new chat session** in the playground, then enter the following prompt:

```
I need to create a PV for our monthly GitHub Copilot subscription. 
The cost is 5,000 baht per month. It's for the IT Internal project.
My name is Somchai Jaidee and the approver is Napat Ruangroj.
```

**Expected behavior:**
- Agent identifies this as a `MonthlyFee` type
- Agent asks for any missing fields (payee, request date, etc.)
- Agent does **not** set `ReadyForSubmission` until user confirms

Take note of which fields the agent asks for and whether the conversation feels natural.

### User Story 2 — One-Time Purchase

Start a **new chat session**, then enter:

```
I want to pay for a one-time purchase of a USB-C hub for my workstation.
The price is 1,200 baht.
```

**Expected behavior:**
- Agent identifies as `OneTime` type
- Agent asks for missing fields one at a time (requestor, payee, project, approver, etc.)
- Agent explicitly asks for the project name — it must **not** invent one

### User Story 3 — Missing Project Name

Start a **new chat session**, then enter:

```
Please create a PV for training workshop registration fee, 3,500 baht,
for Prasert Kaewkla. The approver is Manee Srisuk.
My name is Chokchai Phongphan.
```

**Expected behavior:**
- Agent correctly collects most fields
- Agent **must ask** for the project name — it must not guess or invent it
- Agent waits for user to provide the project name before proceeding

> **Observation question**: Does your agent always ask for missing fields rather than inventing values? If not, adjust the instruction and re-test.

---

## 2.7. Part 4: Implement the Agent in C#

### 2.7.1. Prepare the .NET project

1. In VS Code, navigate to the `labs-dotnet/02-pv-agent/02-agent-foundation/Labfiles/` folder.

1. Right-click the folder and select **Open in Integrated Terminal**.

1. Run the following commands to restore NuGet packages and build the project:

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

    **Windows (Command Prompt):**
    ```cmd
    dotnet restore
    dotnet build
    ```

1. Open the `appsettings.json` file and fill in the values from your Foundry project:

    ```json
    {
      "AzureOpenAI": {
        "Endpoint": "<your-project-endpoint>",
        "DeploymentName": "gpt-4.1",
        "ApiKey": "<your-api-key>"
      }
    }
    ```

    > **Tip**: Reuse the same endpoint and API key you configured in D3-M1.

1. Open the `Program.cs` file. Review the existing structure — it has TODO comments to guide you through each step.

### 2.7.2. Add the PV Agent instruction

1. In `Program.cs`, find the comment `// Define PV Agent instructions` and replace the placeholder string with the instruction you designed in Part 2:

    ```csharp
    // Define PV Agent instructions
    string pvAgentInstructions = """
        You are "PV Agent" — an AI assistant that helps requestors draft a Payment Voucher (PV) request.

        YOUR ROLE:
        - Guide the user through collecting all required PV fields via natural conversation
        - Ask only for missing critical fields, one at a time
        - State your assumptions explicitly (for example, "I'll use THB as the default currency")
        - Show a confirmation summary before marking the PV as ReadyForSubmission
        - Always output a valid JSON object as the final step

        REQUIRED FIELDS (must be collected before ReadyForSubmission):
        - pvTitle: Short, descriptive title for the voucher
        - requestDate: Date in YYYY-MM-DD format
        - requestor.name: Full name of the person making the request
        - payee.name: Full name or company of who will receive the payment
        - purpose.for: What specifically is being paid for
        - purpose.objective: The business reason or objective
        - expense.type: MUST be exactly "MonthlyFee" or "OneTime" — map user's words to one of these two values
        - expense.amount.value: A positive number — if user says text like "one thousand", convert to 1000
        - expense.amount.currency: Default to "THB" unless user specifies otherwise
        - project.projectName: Name of the project this payment belongs to — never invent, always ask user
        - approval.approverName: Name of the approver

        CONSTRAINTS:
        - You do NOT approve requests or change budget limits
        - You do NOT invent project names, payee names, or amounts
        - approval.status is always "Pending" for new requests
        - Set status to "ReadyForSubmission" only after user confirms the summary
        - Keep status as "Draft" otherwise

        OUTPUT FORMAT (produce this JSON when all fields are collected and confirmed):
        {
          "pv": {
            "pvTitle": "...",
            "requestDate": "YYYY-MM-DD",
            "requestor": { "name": "..." },
            "payee": { "name": "..." },
            "purpose": {
              "for": "...",
              "objective": "..."
            },
            "expense": {
              "type": "MonthlyFee | OneTime",
              "amount": { "value": 0, "currency": "THB" }
            },
            "project": {
              "projectName": "...",
              "budgetSummary": {
                "monthlyBudget": 0
              }
            },
            "approval": { "approverName": "...", "status": "Pending" },
            "status": "Draft | ReadyForSubmission"
          }
        }
        """;
    ```

### 2.7.3. Create the AIAgent

1. Find the comment `// TODO: Initialize AIAgent using OpenAIClient with ApiKeyCredential and OpenAIClientOptions` and replace it with:

    ```csharp
    // Create the AIAgent with OpenAI-compatible client and PV Agent instructions
    AIAgent agent = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
        .GetChatClient(deploymentName)
        .AsAIAgent(
            instructions: pvAgentInstructions,
            name: "PVAgent");
    ```

    > **Why `OpenAIClient` instead of `AzureOpenAIClient`?**  
    > The Foundry project provides an **OpenAI-compatible endpoint** (ending in `/openai/v1`). `AzureOpenAIClient` adds an extra path segment that leads to HTTP 404. `OpenAIClient` with a custom `Endpoint` in `OpenAIClientOptions` sends requests directly to the compatible endpoint.

### 2.7.4. Create an AgentSession for multi-turn conversation

1. Find the comment `// TODO: Create an AgentSession from the agent` and replace it with:

    ```csharp
    // Create an AgentSession to maintain conversation history across turns
    AgentSession session = await agent.CreateSessionAsync();
    ```

    > **What is `AgentSession`?**  
    > An `AgentSession` keeps track of the full conversation history for you. Each call to `RunStreamingAsync` with the same session automatically includes previous turns — you don't need to manually manage a message history list.

### 2.7.5. Stream agent responses in the conversation loop

1. Find the comment block:
    ```csharp
    // TODO: Stream the agent response using RunStreamingAsync and print each update
    // Hint: use "await foreach" over agent.RunStreamingAsync(userInput, session)
    ```

    Replace it with:

    ```csharp
    // Stream the agent response and print each update as it arrives
    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userInput, session))
    {
        Console.Write(update.Text);
    }
    ```

1. Save the file (**Ctrl+S**).

### 2.7.6. Run the agent

1. In the terminal, run:

    ```bash
    dotnet run
    ```

1. Test the three user stories from Part 3, observing how the agent handles each case.

1. When you are satisfied with the behavior, type `quit` to exit.

---

## 2.8. Deliverables

After completing this exercise, you should have:

- ✅ An agent named `pv-agent` in your Foundry project with the instruction defined
- ✅ Tested three user story scenarios in the portal playground
- ✅ A working `Program.cs` that runs a multi-turn PV Agent conversation loop
- ✅ Notes on how the agent handles missing fields vs. happy-path inputs

---

## 2.9. Summary

In this exercise, you:

- Defined the PV Agent's role, boundaries, and required fields
- Designed a slot-filling conversation flow
- Wrote and tested an agent instruction in the Foundry portal
- Implemented the agent in C# using `AIAgent` and `AgentSession` from the Microsoft Agent Framework
- Used `RunStreamingAsync` to stream responses to the console in real time
- Validated the design against three realistic user stories

In the next module (**D3-M3**), you'll add a **data grounding layer** that provides the agent with real project and budget data to augment its responses.
