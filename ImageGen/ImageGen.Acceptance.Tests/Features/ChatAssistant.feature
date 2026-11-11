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
