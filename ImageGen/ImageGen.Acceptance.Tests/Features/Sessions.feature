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

  @SES-04 @S1.2
  Scenario: A session title longer than 40 characters is rejected
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user returns to the session list
    And the rename prompt will return "This title is deliberately longer than forty characters"
    And the user renames the first session
    Then the first session retains its original title
    And the app shows a rename error

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

  @SES-08 @S1.2
  Scenario: The session row shows the number of runs
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                         |
      | generate_images | A lighthouse by the ocean.    |
    And the assistant answers with:
      | response        | prompt                         |
      | generate_images | A red bicycle by a brick wall. |
    When the user creates a new session
    And the user sends the message "Paint a lighthouse"
    And the user sends the message "Draw a red bicycle"
    And the user returns to the session list
    Then the first session row shows "· 2 runs"

  @SES-09 @S1.1
  Scenario: Sessions are grouped by the fake local date
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the current time advances by 1 day
    And the user creates a new session
    And the user returns to the session list
    Then one session is in Today and one session is in Earlier

  @SES-10 @S1.1
  Scenario: Choosing an example from the empty session list sends it in a new session
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response | text                    |
      | text     | Let's start with this. |
    When the user chooses the first example from the empty session list
    Then the selected example is sent once

  @SES-11 @S1.4
  Scenario: Declining to clear history keeps sessions and their files
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                    |
      | generate_images | A blue house by the sea.  |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost  |
      | picture  | 64    | 64     | 0.014 |
    When the user creates a new session
    And the user sends the message "Draw a blue house"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    And the generation card shows model "black-forest-labs/flux.2-klein-4b", seed, and cost "$0.014"
    When the user returns to the session list
    And the user opens settings
    And the user declines clearing history
    And the user clears history
    Then the session remains in the list
    And its image files remain

  @SES-11 @S1.4
  Scenario: Clearing history from an open chat deletes sessions and files but keeps keys and settings
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                     |
      | generate_images | A blue house by the sea.   |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost  |
      | picture  | 64    | 64     | 0.014 |
    And the assistant answers with:
      | response        | prompt                 |
      | generate_images | A red boat at sunset. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost  |
      | picture  | 64    | 64     | 0.014 |
    When the user creates a new session
    And the user sends the message "Draw a blue house"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    And the generation card shows model "black-forest-labs/flux.2-klein-4b", seed, and cost "$0.014"
    When the user returns to the session list
    And the user creates a new session
    And the user sends the message "Draw a red boat"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    And the generation card shows model "black-forest-labs/flux.2-klein-4b", seed, and cost "$0.014"
    And two session folders contain generated image files
    When the user chooses the Gallery style and dark theme
    And the user opens settings
    And the user confirms clearing history
    And the user clears history
    Then no sessions are in the list
    And all session image files are deleted
    And the saved OpenRouter key remains
    When the user opens settings
    Then settings show the OpenRouter key ending in "-key"
    And the active appearance is Gallery with the dark theme
    And appearance settings remain Gallery with the dark theme

  @SES-11 @S1.4 @ignore @bug-288
  Scenario: Spending includes the costs of assistant-generated images across sessions
    Given the spending clock is at "2026-01-01T12:00:00+00:00" in time zone "UTC"
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                   |
      | generate_images | A blue house by the sea. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost  |
      | picture  | 64    | 64     | 0.014 |
    And the assistant answers with:
      | response        | prompt                |
      | generate_images | A red boat at sunset. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost  |
      | picture  | 64    | 64     | 0.014 |
    When the user creates a new session
    And the user sends the message "Draw a blue house"
    Then the generation card shows model "black-forest-labs/flux.2-klein-4b", seed, and cost "$0.014"
    When the user returns to the session list
    And the user creates a new session
    And the user sends the message "Draw a red boat"
    Then the generation card shows model "black-forest-labs/flux.2-klein-4b", seed, and cost "$0.014"
    When the user opens the spending screen for the current day
    Then spending total is "$0.028"
