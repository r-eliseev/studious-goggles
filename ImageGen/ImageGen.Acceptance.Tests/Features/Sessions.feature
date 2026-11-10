Feature: Sessions

  @SES-01 @S1.1
  Scenario: A new session opens as an empty chat
    Given the user has a saved OpenRouter key
    When the user creates a new session
    Then an empty session chat is shown

  @SES-06 @S1.3
  Scenario: A new session uses the default assistant model
    Given the user has a saved OpenRouter key
    When the user creates a new session
    Then the chat shows the default assistant model

  @SES-09 @S1.1
  Scenario: Sessions are grouped by the fake local date
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the current time advances by 1 day
    And the user creates a new session
    And the user returns to the session list
    Then one session is in Today and one session is in Earlier


