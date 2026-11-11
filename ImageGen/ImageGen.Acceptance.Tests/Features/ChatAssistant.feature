@chat
Feature: Chat and assistant

  @CHAT-01 @S2.2
  Scenario: The assistant answers with text without starting a run
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response | text |
      | text     | Could you tell me more about the style? |
    When the user creates a new session
    And the user sends the message "Make a red bicycle"
    Then the assistant message "Could you tell me more about the style?" is shown
    And no run is shown in the chat

  @CHAT-02 @S2.2 @S2.3 @F9.4
  Scenario: The generation uses the exact prompt returned by the assistant tool
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response       | prompt                                                        |
      | generate_images | A red bicycle with a brass bell, leaning against a brick wall. |
    When the user creates a new session
    And the user sends the message "Draw a red bicycle"
    Then the assistant tool prompt "A red bicycle with a brass bell, leaning against a brick wall." is preserved in the run, card, and generation request

  @CHAT-03 @S2.1
  Scenario: The assistant receives its system prompt and the last run prompt
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                              |
      | generate_images | A watercolor lighthouse on a rocky coast at sunset. |
    And the assistant answers with:
      | response | text                      |
      | text     | I will warm the lighting. |
    When the user creates a new session
    And the user sends the message "Paint a lighthouse"
    And the user sends the message "Make the lighting warmer"
    Then the latest assistant request contains the system prompt and current run prompt "A watercolor lighthouse on a rocky coast at sunset."

  @CHAT-04 @S2.5
  Scenario Outline: An assistant request error appears in the chat and keeps the user message
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response | status | body                           |
      | error    | <status> | {"error":{"message":"failure"}} |
    When the user creates a new session
    And the user sends the message "Draw a red bicycle"
    Then the user message "Draw a red bicycle" remains in the chat
    And an assistant error message "<error>" is shown

    Examples:
      | status | error                                        |
      | 500    | Something went wrong.                       |
      | 429    | The provider is limiting requests. Try again later. |
      | 401    | The API key was rejected.                   |

  @CHAT-05 @S2.1
  Scenario: A whitespace-only message cannot be sent
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user enters "   " in the chat input
    Then sending a chat message is unavailable
