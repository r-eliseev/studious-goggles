@sessions
Feature: Sessions

  @SES-02 @S1.2
  Scenario: A first message sets the session title to its first 40 characters
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response | text |
      | text     | A message that is deliberately longer than forty characters |
    When the user creates a new session
    And the user sends the message "A message that is deliberately longer than forty characters"
    Then the session title is the first 40 characters of the message

  @SES-03 @S1.2
  Scenario: Renaming a session updates its list and chat title without changing the order
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user creates a new session
    And the user returns to the session list
    And the rename prompt will return "Renamed session"
    And the user remembers the session order
    And the user renames the first session
    And the user opens the first session
    Then the session order is unchanged
    And the chat title is "Renamed session"

  @SES-04 @S1.2
  Scenario: An empty session title is rejected
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user returns to the session list
    And the rename prompt will return ""
    And the user renames the first session
    Then the first session retains its original title
    And the app shows a rename error

  @SES-04 @S1.2 @ignore @bug-276
  Scenario: A session title longer than 40 characters is rejected
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user returns to the session list
    And the rename prompt will return "This title is deliberately longer than forty characters"
    And the user renames the first session
    Then the first session retains its original title

  @SES-05 @S1.1
  Scenario: Deleting a session requires confirmation and survives restart
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user returns to the session list
    And the delete confirmation is declined
    And the user deletes the first session
    Then the first session remains in the list
    When the delete confirmation is accepted
    And the user deletes the first session
    And the user restarts the app
    Then no sessions are in the list

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

  @SES-07 @S1.3
  Scenario: Changing the default assistant model affects only new sessions
    Given the OpenRouter assistant model catalog:
      | id                             | name             |
      | anthropic/claude-haiku-5.5     | Claude Haiku 5.5 |
      | anthropic/claude-sonnet-4      | Claude Sonnet 4  |
    And the user has a saved OpenRouter key
    When the user creates a new session
    And the user returns to the session list
    And the user opens settings
    And the user selects assistant model "anthropic/claude-sonnet-4" as the default
    And the user saves the default assistant model
    And the user returns to the session list
    And the user opens the first session
    Then the chat shows assistant model "anthropic/claude-haiku-5.5"
    When the user returns to the session list
    And the user creates a new session
    Then the chat shows assistant model "anthropic/claude-sonnet-4"

  @SES-09 @S1.1
  Scenario: Sessions are grouped by the fake local date
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the current time advances by 1 day
    And the user creates a new session
    And the user returns to the session list
    Then one session is in Today and one session is in Earlier


