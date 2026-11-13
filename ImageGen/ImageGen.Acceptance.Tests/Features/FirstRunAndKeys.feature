Feature: First launch and keys

  @KEY-01 @S1 @F1.1 @e2e
  Scenario: First launch without a key opens the API key screen
    Given the app is launched for the first time
    Then the API key screen is shown instead of the session list

  @KEY-10 @ADR0031
  Scenario: Requests outside the fake network are rejected
    Given the app is launched for the first time
    When a request is sent to an unconfigured fake route
    Then the fake network rejects it as unexpected

  @KEY-03 @F1.2 @KEY-05 @F1.3 @F10.3
  Scenario: An invalid key is rejected and not saved
    Given the app is launched for the first time
    And OpenRouter rejects key checks with HTTP 401
    When the user enters API key "sk-or-acceptance-invalid"
    And the user checks the API key
    Then the API key status shows it was rejected
    And the entered API key is not saved

  @KEY-04 @F1.2
  Scenario: A connection failure does not save the key
    Given the app is launched for the first time
    And OpenRouter has no connection
    When the user enters API key "sk-or-acceptance-invalid"
    And the user checks the API key
    Then the API key status shows a connection error
    And the entered API key is not saved

  @KEY-02 @F1.2 @S1.3
  Scenario: A valid OpenRouter key opens the session list without assistant model selection
    Given the app is launched for the first time
    When the user enters API key "e2e-mock-key"
    And the user checks the API key
    Then the API key is accepted and the session list opens
    When the user opens settings
    Then the default assistant model is "openrouter/anthropic/claude-haiku-5.5"

  @KEY-12 @S6.4
  Scenario: Choosing another provider on first run opens assistant model selection
    Given the app has a configured chat provider "e2e-mock"
    And the app is launched for the first time
    When the user selects chat provider "e2e-mock"
    And the user enters API key "e2e-mock-key"
    And the user checks the API key
    Then the API key status shows the assistant model choice for "E2E Assistant"
    When the user saves the selected assistant model
    And the user opens settings
    Then the default assistant model is "e2e-mock/e2e/assistant"

  @KEY-06 @F10.3
  Scenario: A saved key remains available after restart
    Given the user has a saved OpenRouter key
    When the user restarts the app
    Then the API key screen stays closed after restart

  @KEY-07 @F1.3 @S6.4
  Scenario: Settings mask the connected key and show other providers as disconnected
    Given the user has a saved OpenRouter key
    When the user opens settings
    Then settings show the OpenRouter key ending in "-key"
    And settings show "fal" as not connected
    And settings show "together" as not connected

  @KEY-08 @F1.3
  Scenario: A rejected replacement keeps the previous key
    Given the user has a saved OpenRouter key
    And OpenRouter rejects key checks with HTTP 401
    When the user opens settings
    And the user changes the OpenRouter key
    And the user enters API key "sk-or-acceptance-invalid"
    And the user checks the API key
    Then the API key status shows it was rejected
    And the previously saved OpenRouter key still works

  @KEY-09 @F1.3
  Scenario: Removing OpenRouter key disconnects the provider
    Given the user has a saved OpenRouter key
    When the user opens settings
    And the user removes the OpenRouter key
    Then settings show "openrouter" as not connected
    When the user restarts the app
    Then the API key screen is shown instead of the session list
