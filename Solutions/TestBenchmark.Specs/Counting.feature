Feature: Counting
	In order to verify this build test
	As a developer
	I want to have some sort of spec I can execute during the build

Scenario: Track usage
	Given I have created a Counter
	And I have called Increment
	When I call Increment again
	Then the last value returned from Increment should be 2
