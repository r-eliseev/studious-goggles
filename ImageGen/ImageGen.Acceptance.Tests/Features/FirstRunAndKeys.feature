Feature: First launch and keys

  @KEY-01 @S1 @F1.1 @e2e
  Scenario: First launch without a key opens the API key screen
    Given the app has no saved OpenRouter key
    When the user starts the app
    Then the API key screen is shown instead of the session list
