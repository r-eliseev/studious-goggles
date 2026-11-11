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
