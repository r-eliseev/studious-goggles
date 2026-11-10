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
